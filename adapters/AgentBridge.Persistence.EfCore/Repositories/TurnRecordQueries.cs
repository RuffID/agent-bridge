using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает обращения только с DialogId родителя, в порядке начала.</summary>
public class TurnRecordQueries(IContextGetItemByPredicateRepository<DialogTurnRecord, AgentBridgeContextKey> reader)
{
    private readonly HistoryQueryGuard? _history;
    private readonly IContextQueryRepository<DialogTurnRecord, AgentBridgeContextKey>? _query;

    /// <summary>Подключает bounded server query для reader и всех write loaders.</summary>
    public TurnRecordQueries(IContextGetItemByPredicateRepository<DialogTurnRecord, AgentBridgeContextKey> reader,
        HistoryQueryGuard history, IContextQueryRepository<DialogTurnRecord, AgentBridgeContextKey> query) : this(reader)
    {
        _history = history;
        _query = query;
    }

    /// <summary>Находит локальный ID обращения вместе с его диалогом.</summary>
    public async Task<DialogTurnRecord?> FindAsync(Guid dialogId, Guid turnId, CancellationToken cancellationToken = default, bool trackChanges = false)
    {
        if (_history is not null)
        {
            List<DialogTurnRecord> rows = await _history.ReadAsync(_query!.Query(!trackChanges)
                .Where(record => record.DialogId == dialogId && record.Id == turnId), cancellationToken, take: 1);
            return rows.SingleOrDefault();
        }

        return await reader.GetItemByPredicateAsync(record => record.DialogId == dialogId && record.Id == turnId,
            asNoTracking: !trackChanges, ct: cancellationToken);
    }

    /// <summary>Читает всю историю обращений без отсечения по compact.</summary>
    public Task<List<DialogTurnRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        _history is not null ? _history.ReadAsync(_query!.Query(true).Where(record => record.DialogId == dialogId)
            .OrderBy(record => record.Sequence), cancellationToken) :
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.Sequence), ct: cancellationToken);
}
