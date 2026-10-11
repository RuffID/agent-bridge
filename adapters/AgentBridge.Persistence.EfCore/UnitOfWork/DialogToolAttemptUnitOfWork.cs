using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Mapping;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogToolAttemptUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogStateLoader loader,
    ModelStepRecordQueries steps, TurnContentStaging content, RecordStaging<ModelStepRecord> stepStaging,
    RecordStaging<DialogRecord> dialogs, DialogCatalogStaging? catalog = null) : IDialogToolAttemptWriter
{
    /// <summary>Сохраняет прежнюю бинарную сигнатуру journal writer.</summary>
    public DialogToolAttemptUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogStateLoader loader,
        ModelStepRecordQueries steps, TurnContentStaging content, RecordStaging<ModelStepRecord> stepStaging,
        RecordStaging<DialogRecord> dialogs) : this(scope, guard, loader, steps, content, stepStaging, dialogs, null) { }

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> StartAsync(DialogRunWriteAccess access, DialogWriteToken expected,
        ToolExecutionIdentity identity, CancellationToken cancellationToken = default) =>
        ChangeAsync(access.Access, expected, identity.Call.TurnId, identity.StepId, identity, null, cancellationToken, access.Lease);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogRunWriteAccess access, DialogWriteToken expected,
        Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default) =>
        ChangeAsync(access.Access, expected, turnId, stepId, null, batch, cancellationToken, access.Lease);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> StartAsync(DialogAccess access, DialogWriteToken expected,
        ToolExecutionIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return ChangeAsync(access, expected, identity.Call.TurnId, identity.StepId, identity, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return ChangeAsync(access, expected, turnId, stepId, null, batch, cancellationToken);
    }

    /// <summary>Проверяет persisted parent/identity и принимает журнал вместе с canonical результатами одной transaction.</summary>
    private Task<ServiceResult<DialogWriteToken>> ChangeAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, Guid stepId, ToolExecutionIdentity? started, ToolExecutionBatch? batch, CancellationToken ct, DialogRunLease? lease = null) =>
        scope.ExecuteAsync<DialogWriteToken>(async cancellationToken =>
        {
            if (lease is not null && lease.TurnId != turnId)
                return Failure(new(ServiceErrorType.Conflict, "run_turn_mismatch"));

            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(access, expected, cancellationToken, lease: lease);
            if (!loaded.Success) return ServiceResult<DialogWriteToken>.Fail(loaded.Error!);
            DialogRecord root = loaded.Data!;
            Dialog dialog = await loader.LoadAsync(root, cancellationToken);
            DialogMutationResult captured = dialog.TryCaptureVersion(access.OwnerId, access.NowUtc, out DialogStateVersion? version);
            if (captured != DialogMutationResult.Success) return Failure(DialogWriteResults.Error(captured));
            DialogMutationResult mutation = dialog.TryAppendTurn(access.OwnerId, version!, turnId, access.NowUtc);
            if (mutation != DialogMutationResult.Success) return Failure(DialogWriteResults.Error(mutation));
            ModelStepRecord? record = await steps.FindAsync(root.Id, turnId, stepId, cancellationToken);
            if (record is null) return Failure(new(ServiceErrorType.NotFound, "Сохранённый шаг не найден."));
            ModelResponse response = record.Response.ToModelResponse();
            if (response.Status != ModelResponseStatus.Completed) return Conflict();
            List<StoredToolAttempt> attempts = [.. ToolAttemptMapping.Read(record.ToolAttemptsJson)];
            if (started is not null)
            {
                if (!Matches(started) || !IsCall(started.OutputIndex) || attempts.Any(item => item.OutputIndex == started.OutputIndex))
                    return Conflict();
                attempts.Add(new(started.OutputIndex, started.Call.AgentId, ToolAttemptState.Started));
            }
            else
            {
                HashSet<int> positions = [];
                foreach (ToolExecutionResult result in batch!.Results)
                {
                    ToolExecutionIdentity identity = result.Identity;
                    if (!Matches(identity) || !IsCall(identity.OutputIndex) || !positions.Add(identity.OutputIndex)) return Conflict();
                    JsonElement call = response.Output[identity.OutputIndex].Content;
                    if (call.GetProperty("call_id").GetString() != result.Invocation.CallId ||
                        call.GetProperty("name").GetString() != result.Invocation.Name) return Conflict();
                    StoredToolAttempt? prior = attempts.SingleOrDefault(item => item.OutputIndex == identity.OutputIndex);
                    if (prior is not null && (prior.State != ToolAttemptState.Started || prior.AgentId != identity.Call.AgentId))
                        return Conflict();
                    if (result.Status is ToolExecutionStatus.Succeeded or ToolExecutionStatus.Unknown && prior is null)
                        return Conflict();
                    if ((result.Status is ToolExecutionStatus.Succeeded or ToolExecutionStatus.Rejected) != (result.Output is not null))
                        return Conflict();
                    ToolAttemptState state = result.Status switch
                    {
                        ToolExecutionStatus.Succeeded => ToolAttemptState.Succeeded,
                        ToolExecutionStatus.Rejected => ToolAttemptState.Rejected,
                        ToolExecutionStatus.Unknown => ToolAttemptState.Unknown,
                        ToolExecutionStatus.NotStarted => ToolAttemptState.NotStarted,
                        _ => throw new InvalidOperationException("Неизвестное состояние инструмента.")
                    };
                    if (prior is not null) attempts.Remove(prior);
                    attempts.Add(new(identity.OutputIndex, identity.Call.AgentId, state));
                }
            }
            ServiceResult<PreparedTurnContent> prepared = await content.PrepareAsync(root.Id, turnId,
                batch?.Outputs ?? Array.Empty<CanonicalModelItem>(), [], cancellationToken);
            if (!prepared.Success) return Failure(prepared.Error!);
            string json = ToolAttemptMapping.Write(attempts);
            long addedBytes = checked(prepared.Data!.AddedBytes + StoredContentSize.Of(json) - StoredContentSize.Of(record.ToolAttemptsJson));
            record.ToolAttemptsJson = json;
            DialogWriteToken next = DialogWriteResults.Apply(root, dialog, addedBytes);
            if (root.CatalogRegistered)
            {
                DialogRuntimeState runtime = DialogRuntimeState.Read(root);
                if (started is not null)
                {
                    DialogRuntimeState.Call call = runtime.Calls.Single(item => item.StepId == stepId && item.OutputIndex == started.OutputIndex);
                    call.State = ToolAttemptState.Started;
                }
                else
                {
                    int outputIndex = 0;
                    foreach (ToolExecutionResult result in batch!.Results)
                    {
                        DialogRuntimeState.Call call = runtime.Calls.Single(item => item.StepId == stepId && item.OutputIndex == result.Identity.OutputIndex);
                        call.State = attempts.Single(item => item.OutputIndex == result.Identity.OutputIndex).State;
                        if (result.Output is not null)
                            call.OutputItemIndex = checked((int)prepared.Data.Items[outputIndex++].Sequence - 1);
                    }
                }

                runtime.Save(root);
                if (catalog is null) throw new InvalidOperationException("Catalog staging required.");
                await catalog.UpdateAsync(root, "run", cancellationToken);
            }

            stepStaging.StageUpdate(record);
            content.Stage(prepared.Data);
            dialogs.StageUpdate(root);
            return ServiceResult<DialogWriteToken>.Ok(next);

            bool Matches(ToolExecutionIdentity identity) => identity.Call.DialogId.Equals(access.DialogId)
                && identity.Call.OwnerId.Equals(access.OwnerId) && identity.IncarnationId == expected.IncarnationId
                && identity.Call.TurnId == turnId && identity.StepId == stepId;
            bool IsCall(int index) => index >= 0 && index < response.Output.Count
                && response.Output[index].Content.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String && type.GetString() == "function_call";
        }, ct);

    /// <summary>Единый безопасный отказ без canonical данных.</summary>
    private static ServiceResult<DialogWriteToken> Conflict() => Failure(new(ServiceErrorType.Conflict, "Попытка несовместима с сохранённым шагом."));
    /// <summary>Сохраняет смысл ожидаемого отказа.</summary>
    private static ServiceResult<DialogWriteToken> Failure(ServiceError error) => ServiceResult<DialogWriteToken>.Fail(error);
}
