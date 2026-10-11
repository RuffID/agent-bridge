using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Mapping;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Durable lifecycle/recovery transactions; не вызывает модель, инструменты, HTTP или evidence I/O.</summary>
public class DialogLifecycleUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogRecordQueries roots,
    DialogStateLoader loader, TurnContentStaging content, RecordStaging<DialogRecord> dialogs,
    RecordStaging<DialogTurnRecord> turns, DialogCatalogQueries queries, DialogCatalogStaging catalog,
    RecordStaging<DialogRecoveryOperationRecord> operations, TimeProvider time, TurnRecordQueries turnQueries,
    IDialogRecoveryEvidenceResolver? evidenceResolver = null, IDialogRunQuiescenceVerifier? quiescenceVerifier = null) : IDialogRunLifecycle, IDialogRecovery
{
    /// <inheritdoc/>
    public Task<ServiceResult<DialogRunState>> BeginAsync(DialogRunBegin request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TurnId == Guid.Empty || request.LeasePeriod <= TimeSpan.Zero || request.LeasePeriod > TimeSpan.FromDays(1))
            return Task.FromResult(Fail<DialogRunState>(ServiceErrorType.Validation, "run_begin_invalid"));
        if (request.Input.Any(IsFunctionItem))
            return Task.FromResult(Fail<DialogRunState>(ServiceErrorType.Validation, "run_input_function_item"));

        return scope.ExecuteAsync<DialogRunState>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(request.Access, request.Expected, ct, catalogControl: true);
            if (!loaded.Success) return ServiceResult<DialogRunState>.Fail(loaded.Error!);
            DialogRecord root = loaded.Data!;
            if (!root.CatalogRegistered) return Fail<DialogRunState>(ServiceErrorType.Unsupported, "catalog_registration_required");
            DialogCatalogRecord metadata = await catalog.RequiredAsync(root, ct);
            if (metadata.RootRevision != root.Revision)
                return Fail<DialogRunState>(ServiceErrorType.Conflict, "catalog_revision_divergence");
            if (!DialogCatalogStaging.Matches(metadata, request.Scope, request.Profile))
                return Fail<DialogRunState>(ServiceErrorType.Forbidden, "catalog_profile_or_scope_mismatch");
            DialogRuntimeState runtime = DialogRuntimeState.Read(root);
            if (runtime.Readiness != DialogReadiness.Ready)
                return Fail<DialogRunState>(ServiceErrorType.Conflict, "dialog_not_ready");

            Dialog dialog = await loader.LoadAsync(root, ct);
            DialogMutationResult mutation = dialog.TryBeginTurn(request.Access.OwnerId, request.TurnId, request.Access.NowUtc, out _);
            if (mutation != DialogMutationResult.Success) return ServiceResult<DialogRunState>.Fail(DialogWriteResults.Error(mutation));
            ServiceResult<PreparedTurnContent> prepared = await content.PrepareAsync(root.Id, request.TurnId, request.Input, [], ct);
            if (!prepared.Success) return ServiceResult<DialogRunState>.Fail(prepared.Error!);
            runtime = new()
            {
                Epoch = checked(runtime.Epoch + 1), RecoveryRevision = runtime.RecoveryRevision,
                TurnId = request.TurnId, Finalized = false, LeaseId = Guid.NewGuid(),
                DeadlineUtc = time.GetUtcNow().Add(request.LeasePeriod)
            };
            runtime.Save(root);
            DialogWriteToken next = DialogWriteResults.Apply(root, dialog, prepared.Data!.AddedBytes);
            DialogTurn turn = dialog.Turns.Single(item => item.Id == request.TurnId);
            ServiceResult projected = await catalog.ApplyContentAsync(root, turn.Id, turn.Sequence, prepared.Data, request.Access.NowUtc, ct);
            if (!projected.Success) return ServiceResult<DialogRunState>.Fail(projected.Error!);
            turns.StageCreate(new()
            {
                DialogId = root.Id, Id = turn.Id, Sequence = turn.Sequence, Status = turn.Status,
                StartedAtUtc = turn.StartedAtUtc, SettingsJson = TurnSettingsMapping.Write(request.Settings)
            });
            content.Stage(prepared.Data);
            dialogs.StageUpdate(root);
            return ServiceResult<DialogRunState>.Ok(new(next, runtime.Lease(root)!));
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<DialogRunLease>> RenewAsync(DialogAccess access, DialogRunLease expected, TimeSpan leasePeriod,
        CancellationToken cancellationToken = default)
    {
        if (leasePeriod <= TimeSpan.Zero || leasePeriod > TimeSpan.FromDays(1))
            return Task.FromResult(Fail<DialogRunLease>(ServiceErrorType.Validation, "lease_period_invalid"));
        return scope.ExecuteAsync<DialogRunLease>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await ReadRootAsync(access, ct);
            if (!loaded.Success) return ServiceResult<DialogRunLease>.Fail(loaded.Error!);
            DialogRecord root = loaded.Data!;
            DialogRuntimeState runtime = DialogRuntimeState.Read(root);
            DateTimeOffset now = time.GetUtcNow();
            if (!runtime.Matches(root, expected, now))
                return Fail<DialogRunLease>(ServiceErrorType.Conflict, "run_lease_stale");
            runtime.DeadlineUtc = now.Add(leasePeriod);
            runtime.Save(root);
            dialogs.StageUpdate(root);
            return ServiceResult<DialogRunLease>.Ok(runtime.Lease(root)!);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<DialogContinuationState>> FinalizeAsync(DialogRunFinalize request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Status) || request.Status == DialogTurnStatus.InProgress)
            return Task.FromResult(Fail<DialogContinuationState>(ServiceErrorType.Validation, "terminal_required"));
        return scope.ExecuteAsync<DialogContinuationState>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(request.Access, request.Expected, ct, lease: request.Lease);
            if (!loaded.Success) return ServiceResult<DialogContinuationState>.Fail(loaded.Error!);
            DialogRecord root = loaded.Data!;
            DialogRuntimeState runtime = DialogRuntimeState.Read(root);
            Dialog dialog = await loader.LoadAsync(root, ct);
            DialogMutationResult captured = dialog.TryCaptureVersion(request.Access.OwnerId, request.Access.NowUtc, out DialogStateVersion? version);
            if (captured != DialogMutationResult.Success) return ServiceResult<DialogContinuationState>.Fail(DialogWriteResults.Error(captured));
            DialogMutationResult mutation = dialog.TryFinishTurn(request.Access.OwnerId, version!, request.Lease.TurnId,
                request.Status, request.Access.NowUtc);
            if (mutation != DialogMutationResult.Success) return ServiceResult<DialogContinuationState>.Fail(DialogWriteResults.Error(mutation));
            runtime.Finalized = true;
            runtime.Terminal = request.Status;
            runtime.LeaseId = Guid.Empty;
            runtime.DeadlineUtc = null;
            runtime.Save(root);
            DialogWriteResults.Apply(root, dialog, 0);
            DialogTurn turn = dialog.Turns.Single(item => item.Id == request.Lease.TurnId);
            // Existing record нужен для сохранения immutable settings при terminal update.
            DialogTurnRecord original = new()
            {
                DialogId = root.Id, Id = turn.Id, Sequence = turn.Sequence, Status = turn.Status,
                StartedAtUtc = turn.StartedAtUtc, FinishedAtUtc = turn.FinishedAtUtc,
                SettingsJson = (await turnQueries.FindAsync(root.Id, turn.Id, ct))?.SettingsJson
            };
            turns.StageUpdate(original);
            await catalog.UpdateAsync(root, "run", ct);
            dialogs.StageUpdate(root);
            return ServiceResult<DialogContinuationState>.Ok(runtime.ToState(root));
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DialogContinuationState>> FenceAsync(DialogRunFence request, CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty || !Enum.IsDefined(request.Reason) ||
            request.Reason == DialogRunFenceReason.ConfirmedQuiescence && !ValidReference(request.EvidenceReference))
            return Fail<DialogContinuationState>(ServiceErrorType.Validation, "fence_request_invalid");
        string hash = Hash(new { Dialog = request.Access.DialogId.Value, Owner = request.Access.OwnerId.Value,
            ExpectedDialog = request.Expected.DialogId.Value, request.Expected.IncarnationId, request.Expected.Revision,
            request.Lease, request.OperationId, request.Reason, request.EvidenceReference });
        if (request.Reason == DialogRunFenceReason.ConfirmedQuiescence)
        {
            ServiceResult<EvidencePreflight> preflight = await PreflightAsync(request.Access, request.Expected,
                request.OperationId, "fence", hash, allowExpired: true, root => FenceError(root, request), cancellationToken);
            if (!preflight.Success) return ServiceResult<DialogContinuationState>.Fail(preflight.Error!);
            if (preflight.Data!.Operation is { } prior) return ServiceResult<DialogContinuationState>.Ok(ReadOperation(prior).Continuation);
            if (quiescenceVerifier is null) return Fail<DialogContinuationState>(ServiceErrorType.Unsupported, "quiescence_verifier_required");
            ServiceResult verified = await quiescenceVerifier.VerifyAsync(request, cancellationToken);
            if (!verified.Success) return ServiceResult<DialogContinuationState>.Fail(verified.Error!);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return await scope.ExecuteAsync<DialogContinuationState>(async ct =>
        {
            ServiceResult<DialogRecord> current = await ReadRootAsync(request.Access, ct, allowExpired: true);
            if (!current.Success) return ServiceResult<DialogContinuationState>.Fail(current.Error!);
            DialogRecord root = current.Data!;
            DialogRecoveryOperationRecord? prior = await queries.OperationAsync(root.Id, request.OperationId, ct);
            if (prior is not null)
                return prior.Kind == "fence" && prior.PayloadHash == hash && prior.IncarnationId == root.IncarnationId
                    ? ServiceResult<DialogContinuationState>.Ok(ReadOperation(prior).Continuation)
                    : Fail<DialogContinuationState>(ServiceErrorType.Conflict, "operation_payload_mismatch");
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(request.Access, request.Expected, ct, allowExpired: true, catalogControl: true);
            if (!loaded.Success) return ServiceResult<DialogContinuationState>.Fail(loaded.Error!);
            DialogRuntimeState runtime = DialogRuntimeState.Read(root);
            ServiceError? fenceError = FenceError(root, request);
            if (fenceError is not null) return ServiceResult<DialogContinuationState>.Fail(fenceError);

            runtime.Epoch = checked(runtime.Epoch + 1);
            runtime.Finalized = true;
            runtime.LeaseId = Guid.Empty;
            runtime.DeadlineUtc = null;
            runtime.Save(root);
            root.Revision = checked(root.Revision + 1);
            dialogs.StageUpdate(root);
            await catalog.UpdateAsync(root, "run", ct);
            operations.StageCreate(Operation(root, runtime, request.OperationId, "fence", hash));
            return ServiceResult<DialogContinuationState>.Ok(runtime.ToState(root));
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DialogRecoveryResult>> RecoverAsync(DialogRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RecoveryId == Guid.Empty || request.TurnId == Guid.Empty || request.Resolutions.Count is < 1 or > 1024 ||
            request.Resolutions.Select(item => (item.TurnId, item.StepId, item.OutputIndex)).Distinct().Count() != request.Resolutions.Count ||
            request.Resolutions.Any(item => !Enum.IsDefined(item.Kind) || item.TurnId != request.TurnId ||
                item.Kind != DialogRecoveryKind.NotStarted && !ValidReference(item.AcknowledgementReference)))
            return Fail<DialogRecoveryResult>(ServiceErrorType.Validation, "recovery_batch_invalid");
        string hash = Hash(new
        {
            Dialog = request.Access.DialogId.Value, Owner = request.Access.OwnerId.Value,
            ExpectedDialog = request.Expected.DialogId.Value, request.Expected.IncarnationId,
            request.Expected.Revision, request.RecoveryId, request.ExpectedRecoveryRevision, request.ExpectedEpoch, request.TurnId,
            Resolutions = request.Resolutions.OrderBy(item => item.StepId).ThenBy(item => item.OutputIndex)
        });
        Dictionary<(Guid Step, int Index), DialogRecoveryEvidence> evidence = [];
        if (request.Resolutions.Any(NeedsEvidence))
        {
            ServiceResult<EvidencePreflight> preflight = await PreflightAsync(request.Access, request.Expected,
                request.RecoveryId, "recovery", hash, allowExpired: false, root => RecoveryError(root, request), cancellationToken);
            if (!preflight.Success) return ServiceResult<DialogRecoveryResult>.Fail(preflight.Error!);
            if (preflight.Data!.Operation is { } prior) return ServiceResult<DialogRecoveryResult>.Ok(ReadOperation(prior));
            if (evidenceResolver is null) return Fail<DialogRecoveryResult>(ServiceErrorType.Unsupported, "recovery_evidence_resolver_required");
            foreach (DialogRecoveryResolution resolution in request.Resolutions.Where(NeedsEvidence))
            {
                ServiceResult<DialogRecoveryEvidence> resolved = await evidenceResolver.ResolveAsync(new(request, resolution), cancellationToken);
                if (!resolved.Success) return ServiceResult<DialogRecoveryResult>.Fail(resolved.Error!);
                cancellationToken.ThrowIfCancellationRequested();
                DialogRecoveryEvidence proof = resolved.Data!;
                if (proof.Kind != resolution.Kind || proof.Reference != resolution.AcknowledgementReference ||
                    (proof.Output is not null) != (resolution.Kind == DialogRecoveryKind.ConfirmedExternalOutcome))
                    return Fail<DialogRecoveryResult>(ServiceErrorType.Conflict, "recovery_evidence_mismatch");
                evidence.Add((resolution.StepId, resolution.OutputIndex), proof);
            }
        }
        return await scope.ExecuteAsync<DialogRecoveryResult>(async ct =>
        {
            ServiceResult<DialogRecord> current = await ReadRootAsync(request.Access, ct);
            if (!current.Success) return ServiceResult<DialogRecoveryResult>.Fail(current.Error!);
            DialogRecord root = current.Data!;
            DialogRecoveryOperationRecord? prior = await queries.OperationAsync(root.Id, request.RecoveryId, ct);
            if (prior is not null)
                return prior.Kind == "recovery" && prior.PayloadHash == hash && prior.IncarnationId == root.IncarnationId
                    ? ServiceResult<DialogRecoveryResult>.Ok(ReadOperation(prior))
                    : Fail<DialogRecoveryResult>(ServiceErrorType.Conflict, "recovery_payload_mismatch");
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(request.Access, request.Expected, ct, catalogControl: true);
            if (!loaded.Success) return ServiceResult<DialogRecoveryResult>.Fail(loaded.Error!);
            DialogRuntimeState runtime = DialogRuntimeState.Read(root);
            if (runtime.LeaseId != Guid.Empty || !runtime.Finalized || runtime.Epoch != request.ExpectedEpoch ||
                runtime.RecoveryRevision != request.ExpectedRecoveryRevision || runtime.TurnId != request.TurnId)
                return Fail<DialogRecoveryResult>(ServiceErrorType.Conflict, "recovery_not_quiescent_or_stale");
            DialogRuntimeState.Call[] pending = runtime.Calls.Where(item => item.OutputItemIndex is null && item.ResolutionJson is null).ToArray();
            if (pending.Length != request.Resolutions.Count)
                return Fail<DialogRecoveryResult>(ServiceErrorType.Conflict, "recovery_full_batch_required");
            long revision = checked(runtime.RecoveryRevision + 1);
            foreach (DialogRuntimeState.Call call in pending)
            {
                DialogRecoveryResolution? resolution = request.Resolutions.SingleOrDefault(item =>
                    item.TurnId == call.TurnId && item.StepId == call.StepId && item.OutputIndex == call.OutputIndex);
                if (resolution is null || resolution.Kind == DialogRecoveryKind.NotStarted && call.State != ToolAttemptState.NotStarted ||
                    resolution.Kind != DialogRecoveryKind.NotStarted && call.State is not (ToolAttemptState.Started or ToolAttemptState.Unknown))
                    return Fail<DialogRecoveryResult>(ServiceErrorType.Conflict, "recovery_outcome_not_proven");
                if (resolution.Kind == DialogRecoveryKind.ConfirmedExternalOutcome)
                {
                    CanonicalModelItem output = evidence[(resolution.StepId, resolution.OutputIndex)].Output!;
                    if (!ValidConfirmedOutput(output, call.CallId))
                        return Fail<DialogRecoveryResult>(ServiceErrorType.Validation, "recovery_evidence_output_invalid");
                    call.ResolutionJson = output.Content.GetRawText();
                }
                else
                {
                    string code = resolution.Kind switch
                    {
                        DialogRecoveryKind.NotStarted => "tool_not_started",
                        DialogRecoveryKind.KnownCanceled => "tool_canceled",
                        _ => "tool_outcome_unknown"
                    };
                    // Synthetic error не имеет успешного output и не изменяет исходный attempt/terminal.
                    call.ResolutionJson = JsonSerializer.Serialize(new
                    {
                        type = "function_call_output", call_id = call.CallId,
                        output = JsonSerializer.Serialize(new { error = new { code }, recovery_id = request.RecoveryId,
                            incarnation_id = root.IncarnationId, turn_id = call.TurnId, step_id = call.StepId,
                            output_index = call.OutputIndex, evidence_reference = resolution.AcknowledgementReference })
                    });
                }
                call.RecoveryId = request.RecoveryId;
                call.ResolutionRevision = revision;
                call.ResolutionKind = resolution.Kind;
                call.EvidenceReference = resolution.AcknowledgementReference;
            }

            runtime.RecoveryRevision = revision;
            runtime.Save(root);
            root.Revision = checked(root.Revision + 1);
            dialogs.StageUpdate(root);
            await catalog.UpdateAsync(root, "recovery", ct);
            DialogRecoveryOperationRecord operation = Operation(root, runtime, request.RecoveryId, "recovery", hash);
            operation.PairsJson = JsonSerializer.Serialize(runtime.Calls);
            operations.StageCreate(operation);
            return ServiceResult<DialogRecoveryResult>.Ok(ReadOperation(operation));
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DialogRecoveryResult>> ReadAsync(DialogAccess access, Guid recoveryId, CancellationToken cancellationToken = default)
    {
        // Чтение тоже проходит scoped gate/transaction для согласованной owner/root/operation проверки.
        return await scope.ExecuteAsync<DialogRecoveryResult>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await ReadRootAsync(access, ct);
            if (!loaded.Success) return ServiceResult<DialogRecoveryResult>.Fail(loaded.Error!);
            DialogRecoveryOperationRecord? operation = await queries.OperationAsync(access.DialogId.Value, recoveryId, ct);
            if (operation is null || operation.Kind != "recovery" || operation.IncarnationId != loaded.Data!.IncarnationId)
                return Fail<DialogRecoveryResult>(ServiceErrorType.NotFound, "recovery_not_found");

            return ServiceResult<DialogRecoveryResult>.Ok(ReadOperation(operation));
        }, cancellationToken);
    }

    private async Task<ServiceResult<DialogRecord>> ReadRootAsync(DialogAccess access, CancellationToken ct, bool allowExpired = false)
    {
        DialogRecord? root = await roots.FindAsync(access.DialogId.Value, ct, trackChanges: true);
        if (root is null) return Fail<DialogRecord>(ServiceErrorType.NotFound, "dialog_not_found");
        ServiceResult<DialogRecord> guarded = await guard.LoadRunAsync(access, new(access.DialogId, root.IncarnationId, root.Revision),
            ct, allowExpired: allowExpired, catalogControl: true);
        if (!guarded.Success) return guarded;
        return root.CatalogRegistered ? guarded : Fail<DialogRecord>(ServiceErrorType.Unsupported, "catalog_registration_required");
    }

    private static bool IsFunctionItem(CanonicalModelItem item) => item.Content.TryGetProperty("type", out JsonElement type) &&
        type.ValueKind == JsonValueKind.String && type.GetString() is "function_call" or "function_call_output";

    private static bool ValidReference(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256;

    private ServiceError? FenceError(DialogRecord root, DialogRunFence request)
    {
        DialogRuntimeState runtime = DialogRuntimeState.Read(root);
        return runtime.LeaseId == Guid.Empty || runtime.LeaseId != request.Lease.LeaseId ||
            runtime.Epoch != request.Lease.Epoch || request.Lease.IncarnationId != root.IncarnationId ||
            runtime.TurnId != request.Lease.TurnId ||
            request.Reason == DialogRunFenceReason.ExpiredLease && runtime.DeadlineUtc > time.GetUtcNow()
            ? new(ServiceErrorType.Conflict, "lease_not_expired_or_stale") : null;
    }

    private static ServiceError? RecoveryError(DialogRecord root, DialogRecoveryRequest request)
    {
        DialogRuntimeState runtime = DialogRuntimeState.Read(root);
        if (runtime.LeaseId != Guid.Empty || !runtime.Finalized || runtime.Epoch != request.ExpectedEpoch ||
            runtime.RecoveryRevision != request.ExpectedRecoveryRevision || runtime.TurnId != request.TurnId)
            return new(ServiceErrorType.Conflict, "recovery_not_quiescent_or_stale");

        DialogRuntimeState.Call[] pending = runtime.Calls.Where(call => call.OutputItemIndex is null && call.ResolutionJson is null).ToArray();
        if (pending.Length != request.Resolutions.Count)
            return new(ServiceErrorType.Conflict, "recovery_full_batch_required");

        foreach (DialogRuntimeState.Call call in pending)
        {
            DialogRecoveryResolution? resolution = request.Resolutions.SingleOrDefault(item =>
                item.TurnId == call.TurnId && item.StepId == call.StepId && item.OutputIndex == call.OutputIndex);
            if (resolution is null || resolution.Kind == DialogRecoveryKind.NotStarted && call.State != ToolAttemptState.NotStarted ||
                resolution.Kind != DialogRecoveryKind.NotStarted && call.State is not (ToolAttemptState.Started or ToolAttemptState.Unknown))
                return new(ServiceErrorType.Conflict, "recovery_outcome_not_proven");
        }

        return null;
    }
    private static bool NeedsEvidence(DialogRecoveryResolution resolution) => resolution.Kind is
        DialogRecoveryKind.KnownCanceled or DialogRecoveryKind.ConfirmedExternalOutcome;
    private static bool ValidConfirmedOutput(CanonicalModelItem item, string callId) =>
        Encoding.UTF8.GetByteCount(item.Content.GetRawText()) <= 1024 * 1024 &&
        item.Content.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String && type.GetString() == "function_call_output" &&
        item.Content.TryGetProperty("call_id", out JsonElement id) && id.ValueKind == JsonValueKind.String && id.GetString() == callId &&
        item.Content.TryGetProperty("output", out JsonElement output) && output.ValueKind == JsonValueKind.String;

    // Короткий read scope завершается до trusted evidence I/O. Финальная transaction повторяет token/epoch guards.
    private Task<ServiceResult<EvidencePreflight>> PreflightAsync(DialogAccess access, DialogWriteToken expected,
        Guid id, string kind, string hash, bool allowExpired, Func<DialogRecord, ServiceError?> validate, CancellationToken cancellationToken) =>
        scope.ExecuteAsync<EvidencePreflight>(async ct =>
        {
            ServiceResult<DialogRecord> root = await ReadRootAsync(access, ct, allowExpired);
            if (!root.Success) return ServiceResult<EvidencePreflight>.Fail(root.Error!);
            DialogRecoveryOperationRecord? prior = await queries.OperationAsync(root.Data!.Id, id, ct);
            if (prior is not null)
                return prior.Kind == kind && prior.PayloadHash == hash && prior.IncarnationId == root.Data.IncarnationId
                    ? ServiceResult<EvidencePreflight>.Ok(new() { Operation = prior })
                    : Fail<EvidencePreflight>(ServiceErrorType.Conflict, "operation_payload_mismatch");
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(access, expected, ct, allowExpired: allowExpired, catalogControl: true);
            if (!loaded.Success) return ServiceResult<EvidencePreflight>.Fail(loaded.Error!);

            ServiceError? invalid = validate(loaded.Data!);
            return invalid is null ? ServiceResult<EvidencePreflight>.Ok(new()) : ServiceResult<EvidencePreflight>.Fail(invalid);
        }, cancellationToken);

    private class EvidencePreflight
    {
        public DialogRecoveryOperationRecord? Operation { get; init; }
    }

    private static string Hash<T>(T payload) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload)));

    private static DialogRecoveryOperationRecord Operation(DialogRecord root, DialogRuntimeState runtime, Guid id, string kind, string hash) => new()
    {
        DialogId = root.Id, Id = id, IncarnationId = root.IncarnationId, Revision = runtime.RecoveryRevision,
        Kind = kind, PayloadHash = hash, ResultJson = JsonSerializer.Serialize(new SavedResult
        {
            DialogId = root.Id, IncarnationId = root.IncarnationId, RootRevision = root.Revision, Runtime = runtime
        })
    };

    internal static DialogRecoveryResult ReadOperation(DialogRecoveryOperationRecord operation)
    {
        SavedResult saved = JsonSerializer.Deserialize<SavedResult>(operation.ResultJson) ?? throw new InvalidOperationException("Operation result missing.");
        DialogRecord root = new() { Id = saved.DialogId, IncarnationId = saved.IncarnationId, Revision = saved.RootRevision };
        List<DialogRuntimeState.Call> calls = JsonSerializer.Deserialize<List<DialogRuntimeState.Call>>(operation.PairsJson)!;
        List<DialogRecoveryRecord> records = [];
        foreach (DialogRuntimeState.Call call in calls.Where(item => item.RecoveryId == operation.Id))
        {
            using JsonDocument document = JsonDocument.Parse(call.ResolutionJson!);
            records.Add(new(operation.Id, call.ResolutionRevision, call.Position(finalized: true), new(document.RootElement),
                call.ResolutionKind ?? throw new InvalidOperationException("Recovery kind missing."), call.EvidenceReference));
        }

        return new(saved.Runtime.ToState(root), records);
    }

    private class SavedResult
    {
        public Guid DialogId { get; set; }
        public Guid IncarnationId { get; set; }
        public long RootRevision { get; set; }
        public DialogRuntimeState Runtime { get; set; } = new();
    }

    private static ServiceResult<T> Fail<T>(ServiceErrorType type, string code) where T : class =>
        ServiceResult<T>.Fail(new(type, code));
}
