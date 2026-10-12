using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает принятые версии compact без изменения или удаления предыдущих состояний.</summary>
public class ContextRecordQueries(IContextGetItemByPredicateRepository<DialogContextRecord, AgentBridgeContextKey> reader)
{
    private readonly HistoryQueryGuard? _history;
    private readonly IContextQueryRepository<DialogContextRecord, AgentBridgeContextKey>? _query;

    /// <summary>Ограничивает active compact и полные версии внутри state loader.</summary>
    public ContextRecordQueries(IContextGetItemByPredicateRepository<DialogContextRecord, AgentBridgeContextKey> reader,
        HistoryQueryGuard history, IContextQueryRepository<DialogContextRecord, AgentBridgeContextKey> query) : this(reader)
    {
        _history = history;
        _query = query;
    }

    /// <summary>Читает активную максимальную Version через сортировку base predicate.</summary>
    public async Task<DialogContextRecord?> ReadActiveAsync(Guid dialogId, CancellationToken cancellationToken = default)
    {
        if (_history is not null)
        {
            List<DialogContextRecord> rows = await _history.ReadAsync(_query!.Query(true).Where(record => record.DialogId == dialogId)
                .OrderByDescending(record => record.Version), cancellationToken, take: 1);
            return rows.SingleOrDefault();
        }

        return await reader.GetItemByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderByDescending(record => record.Version), ct: cancellationToken);
    }

    /// <summary>Читает все сохранённые версии по возрастанию, не только активную.</summary>
    public Task<List<DialogContextRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        _history is not null ? _history.ReadAsync(_query!.Query(true).Where(record => record.DialogId == dialogId)
            .OrderBy(record => record.Version), cancellationToken) :
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.Version), ct: cancellationToken);
}
