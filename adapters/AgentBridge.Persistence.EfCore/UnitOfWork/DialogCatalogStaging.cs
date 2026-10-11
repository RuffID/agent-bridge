using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Library-owned atomic compact projection/feed, вызывается только внутри existing write scope.</summary>
public class DialogCatalogStaging(DialogCatalogQueries queries, RecordStaging<DialogCatalogRecord> catalogs,
    RecordStaging<DialogCatalogClockRecord> clocks, RecordStaging<DialogCatalogChangeRecord> changes,
    IEnumerable<IDialogCatalogProjector> projectors)
{
    /// <summary>Ordinal length-prefixed namespace digest; hash не заменяет точную проверку scope.</summary>
    public static string ScopeKey(DialogCatalogScope scope)
    {
        string value = JsonSerializer.Serialize(new[] { scope.OwnerId.Value, scope.SiteId, scope.AgentId });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>Проверяет explicit bounded policy и exact projector; неизвестная версия не fallback.</summary>
    public ServiceError? Validate(DialogProjectionPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Key) || policy.Key.Length > 128 || policy.Version <= 0 ||
            policy.MaxTitleScalars < 9 || policy.MaxTitleScalars > 1024 || policy.MaxSnippetScalars <= 0 ||
            policy.MaxSnippetScalars > 4096 || policy.MaxMessageBytes <= 0 || policy.MaxMessageBytes > 1048576)
            return new(ServiceErrorType.Validation, "catalog_policy_invalid");

        IDialogCatalogProjector[] matches = projectors.Where(item => item.Key == policy.Key && item.Version == policy.Version).ToArray();
        return matches.Length == 1 ? null : new(ServiceErrorType.Unsupported, "catalog_policy_unknown_or_ambiguous");
    }

    /// <summary>Фиксирует root/profile/policy/empty projection и scope feed, не делает самостоятельный commit.</summary>
    public async Task<DialogCatalogRecord> CreateAsync(DialogRecord root, DialogCatalogCreate request, CancellationToken ct)
    {
        DialogCatalogRecord record = new()
        {
            Id = root.Id, IncarnationId = root.IncarnationId, RootRevision = root.Revision,
            ScopeKey = ScopeKey(request.Scope), IdSortKey = root.Id.ToString("N"),
            OwnerId = root.OwnerId, SiteId = request.Scope.SiteId, AgentId = request.Scope.AgentId,
            CreatedAtUtc = root.CreatedAtUtc, ExpiresAtUtc = request.ExpiresAtUtc, SortTimeUtc = root.CreatedAtUtc,
            ProfileJson = JsonSerializer.Serialize(new { request.Profile.Key, request.Profile.Version, request.Profile.Data }),
            PolicyJson = JsonSerializer.Serialize(request.Policy)
        };
        await StageAsync(record, root, "create", ct, creating: true);
        return record;
    }

    /// <summary>Готовит timestamps/text/call-position journal в той же transaction, что canonical save.</summary>
    public async Task<ServiceResult> ApplyContentAsync(DialogRecord root, Guid turnId, long turnSequence,
        PreparedTurnContent content, DateTimeOffset savedAtUtc, CancellationToken ct)
    {
        if (!root.CatalogRegistered) return ServiceResult.Ok();
        DialogCatalogRecord record = await RequiredAsync(root, ct);
        DialogProjectionPolicy policy = Policy(record);
        ServiceError? invalid = Validate(policy);
        if (invalid is not null) return ServiceResult.Fail(invalid);
        IDialogCatalogProjector projector = projectors.Single(item => item.Key == policy.Key && item.Version == policy.Version);
        foreach (CanonicalItemRecord item in content.Items)
        {
            using JsonDocument document = JsonDocument.Parse(item.ContentJson);
            JsonElement canonical = document.RootElement;
            string? role = canonical.TryGetProperty("role", out JsonElement roleValue) && roleValue.ValueKind == JsonValueKind.String
                ? roleValue.GetString() : null;
            string? type = canonical.TryGetProperty("type", out JsonElement typeValue) && typeValue.ValueKind == JsonValueKind.String
                ? typeValue.GetString() : null;
            if (role is not ("user" or "assistant") || type is not (null or "message")) continue;
            if (Encoding.UTF8.GetByteCount(item.ContentJson) > policy.MaxMessageBytes)
                return ServiceResult.Fail(new(ServiceErrorType.Validation, "catalog_message_budget"));

            DialogCatalogTextState prior = Text(record);
            ServiceResult<DialogCatalogTextUpdate> projected = projector.Project(
                new(root.Id, root.IncarnationId, turnId, turnSequence, item.Sequence, savedAtUtc, role, new(canonical)), prior, policy);
            if (!projected.Success) return ServiceResult.Fail(projected.Error!);
            DialogCatalogTextState next = projected.Data!.Text;
            if (next.Title.EnumerateRunes().Count() > policy.MaxTitleScalars ||
                next.Snippet.EnumerateRunes().Count() > policy.MaxSnippetScalars ||
                string.IsNullOrWhiteSpace(next.Title) || next.SearchKey != DialogCatalogText.SearchKey(next.Title) ||
                prior.HasSavedQuestion && (prior.Title != next.Title || !next.HasSavedQuestion) ||
                role == "assistant" && (prior.Title != next.Title || prior.HasSavedQuestion != next.HasSavedQuestion) ||
                role == "user" && !next.HasSavedQuestion)
                return ServiceResult.Fail(new(ServiceErrorType.Validation, "catalog_projection_contract"));

            if (!prior.HasSavedQuestion && next.HasSavedQuestion)
                record.FirstQuestionPosition = $"{turnSequence}/{item.Sequence}";
            record.Title = next.Title;
            record.SearchKey = next.SearchKey;
            record.Snippet = next.Snippet;
            record.LastMessagePosition = $"{turnSequence}/{item.Sequence}";
            record.LastMessageAtUtc = savedAtUtc;
            record.SortTimeUtc = savedAtUtc;
            item.SavedAtUtc = savedAtUtc;
        }

        DialogRuntimeState runtime = DialogRuntimeState.Read(root);
        int offset = 0;
        foreach (ModelStepRecord step in content.Steps)
        {
            ModelResponse response = step.Response.ToModelResponse();
            foreach (CanonicalModelItem output in response.Output)
            {
                if (offset >= content.Items.Count || output.Content.GetRawText() != content.Items[offset].ContentJson)
                    return ServiceResult.Fail(new(ServiceErrorType.Validation, "catalog_step_items_mismatch"));
                JsonElement value = output.Content;
                if (value.TryGetProperty("type", out JsonElement outputType) && outputType.ValueKind == JsonValueKind.String &&
                    outputType.GetString() == "function_call_output")
                    return ServiceResult.Fail(new(ServiceErrorType.Validation, "model_function_output_without_attempt"));

                if (value.TryGetProperty("type", out JsonElement type) && type.GetString() == "function_call")
                {
                    if (!value.TryGetProperty("call_id", out JsonElement id) || id.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 256)
                        return ServiceResult.Fail(new(ServiceErrorType.Validation, "function_call_identity"));
                    runtime.Calls.Add(new()
                    {
                        TurnId = turnId, StepId = step.Id, OutputIndex = offset - content.Steps.TakeWhile(item => item.Id != step.Id)
                            .Sum(item => item.Response.ToModelResponse().Output.Count),
                        ItemIndex = checked((int)content.Items[offset].Sequence - 1), CallId = id.GetString()!
                    });
                }

                offset++;
            }
        }

        if (runtime.Calls.Count > 1024) return ServiceResult.Fail(new(ServiceErrorType.Validation, "pending_call_budget"));
        runtime.Save(root);
        await StageAsync(record, root, content.Items.Any(item => item.SavedAtUtc is not null) ? "message" : "run", ct);
        return ServiceResult.Ok();
    }

    /// <summary>Atomic status/readiness/feed без изменения saved text/timestamp.</summary>
    public async Task UpdateAsync(DialogRecord root, string kind, CancellationToken ct)
    {
        if (!root.CatalogRegistered) return;
        await StageAsync(await RequiredAsync(root, ct), root, kind, ct);
    }

    /// <summary>Delete/expiry оставляют durable tombstone, исключающий recreate и поздний upsert.</summary>
    public async Task DeleteAsync(DialogRecord root, string kind, CancellationToken ct)
    {
        if (!root.CatalogRegistered) return;
        DialogCatalogRecord record = await RequiredAsync(root, ct);
        record.Deleted = true;
        record.Title = string.Empty;
        record.SearchKey = string.Empty;
        record.Snippet = string.Empty;
        await StageAsync(record, root, kind, ct);
    }

    /// <summary>Проверяет наличие и согласованную incarnation/revision компактного индекса.</summary>
    public async Task<DialogCatalogRecord> RequiredAsync(DialogRecord root, CancellationToken ct)
    {
        DialogCatalogRecord record = await queries.FindAsync(root.Id, ct, tracking: true) ??
            throw new InvalidOperationException("Registered root без catalog projection.");
        if (record.Deleted || record.IncarnationId != root.IncarnationId || record.RootRevision > root.Revision)
            throw new InvalidOperationException("Catalog/root divergence.");

        return record;
    }

    /// <summary>Проверяет full immutable namespace/profile; actual права отдельно проверяет trusted host.</summary>
    public static bool Matches(DialogCatalogRecord record, DialogCatalogScope scope, CatalogAccessProfile profile) =>
        record.OwnerId == scope.OwnerId.Value && record.SiteId == scope.SiteId && record.AgentId == scope.AgentId &&
        Profile(record).Matches(profile);

    /// <summary>Материализует только compact DTO, скрывая expired/deleted текст.</summary>
    public static DialogCatalogState State(DialogCatalogRecord record, DateTimeOffset nowUtc)
    {
        bool hidden = record.Deleted || record.ExpiresAtUtc <= nowUtc;
        return new(new(DialogId.From(record.Id), record.IncarnationId, record.RootRevision),
            new(DialogOwnerId.From(record.OwnerId), record.SiteId, record.AgentId), Profile(record), Policy(record),
            hidden ? new(string.Empty, string.Empty, string.Empty, record.FirstQuestionPosition is not null) : Text(record),
            record.CreatedAtUtc, record.LastMessageAtUtc, record.ExpiresAtUtc, record.Deleted,
            (DialogReadiness)record.Readiness, record.Revision, record.RecoveryRevision, record.Epoch,
            record.FirstQuestionPosition, record.LastMessagePosition, record.LastTurnId,
            record.LastTurnStatus is int status ? (DialogTurnStatus)status : null);
    }

    private async Task StageAsync(DialogCatalogRecord record, DialogRecord root, string kind, CancellationToken ct, bool creating = false)
    {
        DialogRuntimeState runtime = DialogRuntimeState.Read(root);
        record.RootRevision = root.Revision;
        record.Revision = checked(record.Revision + 1);
        record.Readiness = (int)runtime.Readiness;
        record.Epoch = runtime.Epoch;
        record.RecoveryRevision = runtime.RecoveryRevision;
        record.LastTurnId = runtime.TurnId;
        record.LastTurnStatus = runtime.TurnId is null ? null : (int)(runtime.Terminal ?? DialogTurnStatus.InProgress);
        DialogCatalogClockRecord? clock = await queries.ClockAsync(record.ScopeKey, ct, tracking: true);
        if (clock is null)
        {
            clock = new() { Id = record.ScopeKey };
            clocks.StageCreate(clock);
        }
        else
        {
            clocks.StageUpdate(clock);
        }

        clock.Sequence = checked(clock.Sequence + 1);
        if (creating) catalogs.StageCreate(record);
        else catalogs.StageUpdate(record);
        changes.StageCreate(new()
        {
            ScopeKey = record.ScopeKey, Sequence = clock.Sequence, Kind = kind, StateJson = JsonSerializer.Serialize(record)
        });
    }

    private static DialogCatalogTextState Text(DialogCatalogRecord record) =>
        new(record.Title, record.SearchKey, record.Snippet, record.FirstQuestionPosition is not null);

    private static DialogProjectionPolicy Policy(DialogCatalogRecord record) =>
        JsonSerializer.Deserialize<DialogProjectionPolicy>(record.PolicyJson) ?? throw new InvalidOperationException("Catalog policy missing.");

    private static CatalogAccessProfile Profile(DialogCatalogRecord record)
    {
        using JsonDocument document = JsonDocument.Parse(record.ProfileJson);
        return new(document.RootElement.GetProperty("Key").GetString()!, document.RootElement.GetProperty("Version").GetInt32(),
            document.RootElement.GetProperty("Data"));
    }
}
