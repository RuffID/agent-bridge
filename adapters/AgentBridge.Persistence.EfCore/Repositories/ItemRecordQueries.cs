using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает полные канонические items с фильтром диалога/обращения и сохранённым порядком.</summary>
public class ItemRecordQueries(IContextGetItemByPredicateRepository<CanonicalItemRecord, AgentBridgeContextKey> reader)
{
    private readonly HistoryQueryGuard? _history;
    private readonly IContextQueryRepository<CanonicalItemRecord, AgentBridgeContextKey>? _query;

    /// <summary>Подключает bounded server query без замены базового repository.</summary>
    public ItemRecordQueries(IContextGetItemByPredicateRepository<CanonicalItemRecord, AgentBridgeContextKey> reader,
        HistoryQueryGuard history, IContextQueryRepository<CanonicalItemRecord, AgentBridgeContextKey> query) : this(reader)
    {
        _history = history;
        _query = query;
    }

    /// <summary>Читает одно обращение, включая результаты инструментов и неизвестные поля.</summary>
    public Task<List<CanonicalItemRecord>> ReadTurnAsync(Guid dialogId, Guid turnId, CancellationToken cancellationToken = default) =>
        _history is not null ? _history.ReadAsync(_query!.Query(true)
            .Where(record => record.DialogId == dialogId && record.TurnId == turnId).OrderBy(record => record.Sequence), cancellationToken) :
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId && record.TurnId == turnId,
            asNoTracking: true, include: query => query.OrderBy(record => record.Sequence), ct: cancellationToken);

    /// <summary>Читает всю историю диалога одним base-запросом; порядок внутри каждого обращения — Sequence.</summary>
    public Task<List<CanonicalItemRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        _history is not null ? _history.ReadAsync(_query!.Query(true).Where(record => record.DialogId == dialogId)
            .OrderBy(record => record.TurnId).ThenBy(record => record.Sequence), cancellationToken) :
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.TurnId).ThenBy(record => record.Sequence), ct: cancellationToken);
}
