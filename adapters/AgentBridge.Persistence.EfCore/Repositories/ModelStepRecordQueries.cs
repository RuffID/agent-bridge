using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает полные отчёты шагов с обоими родителями локального ID.</summary>
public class ModelStepRecordQueries(IContextGetItemByPredicateRepository<ModelStepRecord, AgentBridgeContextKey> reader)
{
    private readonly HistoryQueryGuard? _history;
    private readonly IContextQueryRepository<ModelStepRecord, AgentBridgeContextKey>? _query;

    /// <summary>Подключает bounded full-report reads, включая writer/tool-attempt paths.</summary>
    public ModelStepRecordQueries(IContextGetItemByPredicateRepository<ModelStepRecord, AgentBridgeContextKey> reader,
        HistoryQueryGuard history, IContextQueryRepository<ModelStepRecord, AgentBridgeContextKey> query) : this(reader)
    {
        _history = history;
        _query = query;
    }

    /// <summary>Читает шаги конкретного обращения с обоими родителями локального ID.</summary>
    public Task<List<ModelStepRecord>> ReadTurnAsync(Guid dialogId, Guid turnId, CancellationToken cancellationToken = default) =>
        _history is not null ? _history.ReadAsync(_query!.Query(true)
            .Where(record => record.DialogId == dialogId && record.TurnId == turnId).OrderBy(record => record.Sequence), cancellationToken) :
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId && record.TurnId == turnId,
            asNoTracking: true, include: query => query.OrderBy(record => record.Sequence), ct: cancellationToken);

    /// <summary>Находит шаг только внутри заданного диалога и обращения.</summary>
    public async Task<ModelStepRecord?> FindAsync(Guid dialogId, Guid turnId, Guid stepId, CancellationToken cancellationToken = default)
    {
        if (_history is not null)
        {
            List<ModelStepRecord> rows = await _history.ReadAsync(_query!.Query(true)
                .Where(record => record.DialogId == dialogId && record.TurnId == turnId && record.Id == stepId), cancellationToken, take: 1);
            return rows.SingleOrDefault();
        }

        return await reader.GetItemByPredicateAsync(record => record.DialogId == dialogId && record.TurnId == turnId && record.Id == stepId,
            asNoTracking: true, ct: cancellationToken);
    }

    /// <summary>Читает шаги диалога одним base-запросом; порядок внутри обращения — Sequence выполнения.</summary>
    public Task<List<ModelStepRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        _history is not null ? _history.ReadAsync(_query!.Query(true).Where(record => record.DialogId == dialogId)
            .OrderBy(record => record.TurnId).ThenBy(record => record.Sequence), cancellationToken) :
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.TurnId).ThenBy(record => record.Sequence), ct: cancellationToken);
}
