using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Bounded base queries компактного индекса/журнала; canonical snapshot здесь недоступен.</summary>
public class DialogCatalogQueries(
    IContextGetItemByPredicateRepository<DialogCatalogRecord, AgentBridgeContextKey> catalogs,
    IContextGetItemByPredicateRepository<DialogCatalogClockRecord, AgentBridgeContextKey> clocks,
    IContextGetItemByPredicateRepository<DialogCatalogChangeRecord, AgentBridgeContextKey> changes,
    IContextGetItemByPredicateRepository<DialogRecoveryOperationRecord, AgentBridgeContextKey> operations)
{
    /// <summary>Один compact state.</summary>
    public Task<DialogCatalogRecord?> FindAsync(Guid id, CancellationToken ct, bool tracking = false) =>
        catalogs.GetItemByPredicateAsync(row => row.Id == id, asNoTracking: !tracking, ct: ct);

    /// <summary>Один bounded keyset query, UUID hex DESC, без count/refill.</summary>
    public Task<List<DialogCatalogRecord>> ReadAsync(string scopeKey, DialogCatalogRead request, CancellationToken ct)
    {
        DateTimeOffset? sort = request.After?.SortTimeUtc;
        string? id = request.After?.DialogId.ToString("N");
        return catalogs.GetItemsByPredicateAsync(row => row.ScopeKey == scopeKey && !row.Deleted &&
            (row.ExpiresAtUtc == null || row.ExpiresAtUtc > request.NowUtc) &&
            (sort == null || row.SortTimeUtc < sort || row.SortTimeUtc == sort && row.IdSortKey.CompareTo(id) < 0),
            take: request.Limit, asNoTracking: true,
            include: query => query.OrderByDescending(row => row.SortTimeUtc).ThenByDescending(row => row.IdSortKey), ct: ct);
    }

    /// <summary>Один scope clock.</summary>
    public Task<DialogCatalogClockRecord?> ClockAsync(string scopeKey, CancellationToken ct, bool tracking = false) =>
        clocks.GetItemByPredicateAsync(row => row.Id == scopeKey, asNoTracking: !tracking, ct: ct);

    /// <summary>Одна bounded страница ordered feed.</summary>
    public Task<List<DialogCatalogChangeRecord>> ChangesAsync(string scopeKey, long after, int limit, CancellationToken ct) =>
        changes.GetItemsByPredicateAsync(row => row.ScopeKey == scopeKey && row.Sequence > after, take: limit,
            asNoTracking: true, include: query => query.OrderBy(row => row.Sequence), ct: ct);

    /// <summary>Один прежний operation ID без retry handler.</summary>
    public Task<DialogRecoveryOperationRecord?> OperationAsync(Guid dialogId, Guid operationId, CancellationToken ct) =>
        operations.GetItemByPredicateAsync(row => row.DialogId == dialogId && row.Id == operationId, asNoTracking: true, ct: ct);

    /// <summary>Bounded recovery provenance для full snapshot; overflow отклоняется caller.</summary>
    public Task<List<DialogRecoveryOperationRecord>> RecoveryHistoryAsync(Guid dialogId, CancellationToken ct) =>
        operations.GetItemsByPredicateAsync(row => row.DialogId == dialogId && row.Kind == "recovery", take: 4097,
            asNoTracking: true, include: query => query.OrderBy(row => row.Revision), ct: ct);
}
