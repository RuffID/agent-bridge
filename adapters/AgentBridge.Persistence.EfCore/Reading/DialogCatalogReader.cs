using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;

namespace AgentBridge.Persistence.EfCore.Reading;

/// <summary>Compact catalogue/feed/readiness через scoped gate, без full snapshot scans.</summary>
public class DialogCatalogReader(DialogCatalogQueries queries, DialogRecordQueries roots, PersistenceOperationGate gate)
    : IDialogCatalogReader, IDialogCatalogChangeReader, IDialogContinuationReader
{
    /// <inheritdoc/>
    public async Task<ServiceResult<DialogCatalogSlice>> ReadAsync(DialogCatalogRead request, CancellationToken cancellationToken = default)
    {
        using IDisposable operation = gate.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Valid(request.Scope, request.Limit, request.NowUtc) || request.After is { } cursor &&
            (cursor.Scope != request.Scope || cursor.SortTimeUtc.Offset != TimeSpan.Zero || cursor.DialogId == Guid.Empty))
            return Fail<DialogCatalogSlice>(ServiceErrorType.Validation, "catalog_cursor_or_budget_invalid");
        List<DialogCatalogRecord> candidates = await queries.ReadAsync(DialogCatalogStaging.ScopeKey(request.Scope), request, cancellationToken);
        List<DialogCatalogState> states = [];
        foreach (DialogCatalogRecord candidate in candidates)
        {
            DialogCatalogState state = DialogCatalogStaging.State(candidate, request.NowUtc);
            if (state.Scope != request.Scope) return Fail<DialogCatalogSlice>(ServiceErrorType.Forbidden, "catalog_namespace_mismatch");
            states.Add(state);
        }

        DialogCatalogState? last = states.LastOrDefault();
        return ServiceResult<DialogCatalogSlice>.Ok(new(states, last is null ? null :
            new(request.Scope, last.SortTimeUtc, last.Token.DialogId.Value)));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DialogCatalogState>> ReadStateAsync(DialogAccess access, CancellationToken cancellationToken = default)
    {
        using IDisposable operation = gate.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        DialogCatalogRecord? record = await queries.FindAsync(access.DialogId.Value, cancellationToken);
        if (record is null) return Fail<DialogCatalogState>(ServiceErrorType.NotFound, "catalog_not_found");
        if (record.OwnerId != access.OwnerId.Value) return Fail<DialogCatalogState>(ServiceErrorType.Forbidden, "catalog_owner_mismatch");
        return ServiceResult<DialogCatalogState>.Ok(DialogCatalogStaging.State(record, access.NowUtc));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DialogCatalogChangeSlice>> ReadChangesAsync(DialogCatalogChangeRead request, CancellationToken cancellationToken = default)
    {
        using IDisposable operation = gate.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Valid(request.Scope, request.Limit, request.NowUtc) || request.Checkpoint < 0)
            return Fail<DialogCatalogChangeSlice>(ServiceErrorType.Validation, "catalog_feed_budget_invalid");
        string key = DialogCatalogStaging.ScopeKey(request.Scope);
        DialogCatalogClockRecord? clock = await queries.ClockAsync(key, cancellationToken);
        long last = clock?.Sequence ?? 0;
        if (request.Checkpoint > last) return Fail<DialogCatalogChangeSlice>(ServiceErrorType.Unsupported, "catalog_reset_required");
        List<DialogCatalogChangeRecord> rows = await queries.ChangesAsync(key, request.Checkpoint, request.Limit, cancellationToken);
        long examined = request.Checkpoint;
        List<DialogCatalogChange> changes = [];
        foreach (DialogCatalogChangeRecord row in rows)
        {
            if (row.Sequence != ++examined)
                return Fail<DialogCatalogChangeSlice>(ServiceErrorType.Unsupported, "catalog_reset_required");
            DialogCatalogRecord metadata = JsonSerializer.Deserialize<DialogCatalogRecord>(row.StateJson) ??
                throw new InvalidOperationException("Change projection missing.");
            DialogCatalogState state = DialogCatalogStaging.State(metadata, request.NowUtc);
            if (state.Scope != request.Scope) return Fail<DialogCatalogChangeSlice>(ServiceErrorType.Forbidden, "catalog_namespace_mismatch");
            changes.Add(new(row.Sequence, row.Kind, state));
        }

        if (examined < last && rows.Count < request.Limit)
            return Fail<DialogCatalogChangeSlice>(ServiceErrorType.Unsupported, "catalog_reset_required");
        return ServiceResult<DialogCatalogChangeSlice>.Ok(new(changes, examined));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DialogContinuationState>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
    {
        using IDisposable operation = gate.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        DialogRecord? root = await roots.FindAsync(access.DialogId.Value, cancellationToken);
        if (root is null) return Fail<DialogContinuationState>(ServiceErrorType.NotFound, "dialog_not_found");
        if (root.OwnerId != access.OwnerId.Value) return Fail<DialogContinuationState>(ServiceErrorType.Forbidden, "dialog_owner_mismatch");
        if (!root.CatalogRegistered) return Fail<DialogContinuationState>(ServiceErrorType.Unsupported, "catalog_registration_required");
        if (root.ExpiresAtUtc != DateTimeOffset.MaxValue && root.ExpiresAtUtc <= access.NowUtc)
            return Fail<DialogContinuationState>(ServiceErrorType.Expired, "dialog_expired");
        return ServiceResult<DialogContinuationState>.Ok(DialogRuntimeState.Read(root).ToState(root));
    }

    private static bool Valid(DialogCatalogScope scope, int limit, DateTimeOffset nowUtc) =>
        DialogCatalogCreationUnitOfWork.ValidScope(scope) && limit is >= 1 and <= 256 && nowUtc.Offset == TimeSpan.Zero;
    private static ServiceResult<T> Fail<T>(ServiceErrorType type, string code) where T : class => ServiceResult<T>.Fail(new(type, code));
}
