using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Bounded base queries компактного индекса/журнала; canonical snapshot здесь недоступен.</summary>
public class DialogCatalogQueries(
    IContextGetItemByPredicateRepository<DialogCatalogRecord, AgentBridgeContextKey> catalogs,
    IContextGetItemByPredicateRepository<DialogCatalogClockRecord, AgentBridgeContextKey> clocks,
    IContextGetItemByPredicateRepository<DialogCatalogChangeRecord, AgentBridgeContextKey> changes,
    IContextGetItemByPredicateRepository<DialogRecoveryOperationRecord, AgentBridgeContextKey> operations)
{
    private readonly HistoryQueryGuard? _history;
    private readonly IContextQueryRepository<DialogCatalogRecord, AgentBridgeContextKey>? _catalogQuery;
    private readonly IContextQueryRepository<DialogCatalogClockRecord, AgentBridgeContextKey>? _clockQuery;
    private readonly IContextQueryRepository<DialogCatalogChangeRecord, AgentBridgeContextKey>? _changeQuery;
    private readonly IContextQueryRepository<DialogRecoveryOperationRecord, AgentBridgeContextKey>? _operationQuery;

    /// <summary>Общий bounded gate включает profile/policy/feed и recovery/fence provenance.</summary>
    public DialogCatalogQueries(
        IContextGetItemByPredicateRepository<DialogCatalogRecord, AgentBridgeContextKey> catalogs,
        IContextGetItemByPredicateRepository<DialogCatalogClockRecord, AgentBridgeContextKey> clocks,
        IContextGetItemByPredicateRepository<DialogCatalogChangeRecord, AgentBridgeContextKey> changes,
        IContextGetItemByPredicateRepository<DialogRecoveryOperationRecord, AgentBridgeContextKey> operations,
        HistoryQueryGuard history,
        IContextQueryRepository<DialogCatalogRecord, AgentBridgeContextKey> catalogQuery,
        IContextQueryRepository<DialogCatalogClockRecord, AgentBridgeContextKey> clockQuery,
        IContextQueryRepository<DialogCatalogChangeRecord, AgentBridgeContextKey> changeQuery,
        IContextQueryRepository<DialogRecoveryOperationRecord, AgentBridgeContextKey> operationQuery)
        : this(catalogs, clocks, changes, operations)
    {
        _history = history;
        _catalogQuery = catalogQuery;
        _clockQuery = clockQuery;
        _changeQuery = changeQuery;
        _operationQuery = operationQuery;
    }

    /// <summary>Один compact state.</summary>
    public async Task<DialogCatalogRecord?> FindAsync(Guid id, CancellationToken ct, bool tracking = false)
    {
        if (_history is not null)
        {
            return (await _history.ReadAsync(_catalogQuery!.Query(!tracking).Where(row => row.Id == id), ct, take: 1)).SingleOrDefault();
        }

        return await catalogs.GetItemByPredicateAsync(row => row.Id == id, asNoTracking: !tracking, ct: ct);
    }

    /// <summary>Один bounded keyset query, UUID hex DESC, без count/refill.</summary>
    public Task<List<DialogCatalogRecord>> ReadAsync(string scopeKey, DialogCatalogRead request, CancellationToken ct)
    {
        DateTimeOffset? sort = request.After?.SortTimeUtc;
        string? id = request.After?.DialogId.ToString("N");
        if (_history is not null)
        {
            return _history.ReadAsync(_catalogQuery!.Query(true).Where(row => row.ScopeKey == scopeKey && !row.Deleted &&
                    (row.ExpiresAtUtc == null || row.ExpiresAtUtc > request.NowUtc) &&
                    (sort == null || row.SortTimeUtc < sort || row.SortTimeUtc == sort && row.IdSortKey.CompareTo(id) < 0))
                .OrderByDescending(row => row.SortTimeUtc).ThenByDescending(row => row.IdSortKey), ct, take: request.Limit);
        }

        return catalogs.GetItemsByPredicateAsync(row => row.ScopeKey == scopeKey && !row.Deleted &&
            (row.ExpiresAtUtc == null || row.ExpiresAtUtc > request.NowUtc) &&
            (sort == null || row.SortTimeUtc < sort || row.SortTimeUtc == sort && row.IdSortKey.CompareTo(id) < 0),
            take: request.Limit, asNoTracking: true,
            include: query => query.OrderByDescending(row => row.SortTimeUtc).ThenByDescending(row => row.IdSortKey), ct: ct);
    }

    /// <summary>Один scope clock.</summary>
    public async Task<DialogCatalogClockRecord?> ClockAsync(string scopeKey, CancellationToken ct, bool tracking = false)
    {
        if (_history is not null)
        {
            return (await _history.ReadAsync(_clockQuery!.Query(!tracking).Where(row => row.Id == scopeKey), ct, take: 1)).SingleOrDefault();
        }

        return await clocks.GetItemByPredicateAsync(row => row.Id == scopeKey, asNoTracking: !tracking, ct: ct);
    }

    /// <summary>Одна bounded страница ordered feed.</summary>
    public Task<List<DialogCatalogChangeRecord>> ChangesAsync(string scopeKey, long after, int limit, CancellationToken ct) =>
        _history is not null ? _history.ReadAsync(_changeQuery!.Query(true).Where(row => row.ScopeKey == scopeKey && row.Sequence > after)
            .OrderBy(row => row.Sequence), ct, take: limit) :
        changes.GetItemsByPredicateAsync(row => row.ScopeKey == scopeKey && row.Sequence > after, take: limit,
            asNoTracking: true, include: query => query.OrderBy(row => row.Sequence), ct: ct);

    /// <summary>Один прежний operation ID без retry handler.</summary>
    public async Task<DialogRecoveryOperationRecord?> OperationAsync(Guid dialogId, Guid operationId, CancellationToken ct)
    {
        if (_history is not null)
        {
            return (await _history.ReadAsync(_operationQuery!.Query(true)
                .Where(row => row.DialogId == dialogId && row.Id == operationId), ct, take: 1)).SingleOrDefault();
        }

        return await operations.GetItemByPredicateAsync(row => row.DialogId == dialogId && row.Id == operationId, asNoTracking: true, ct: ct);
    }

    /// <summary>Bounded recovery provenance для full snapshot; overflow отклоняется caller.</summary>
    public Task<List<DialogRecoveryOperationRecord>> RecoveryHistoryAsync(Guid dialogId, CancellationToken ct) =>
        _history is not null ? _history.ReadAsync(_operationQuery!.Query(true).Where(row => row.DialogId == dialogId && row.Kind == "recovery")
            .OrderBy(row => row.Revision), ct) :
        operations.GetItemsByPredicateAsync(row => row.DialogId == dialogId && row.Kind == "recovery", take: 4097,
            asNoTracking: true, include: query => query.OrderBy(row => row.Revision), ct: ct);
}
