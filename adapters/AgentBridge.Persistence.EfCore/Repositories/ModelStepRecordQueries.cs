using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает полные отчёты шагов с обоими родителями локального ID.</summary>
public class ModelStepRecordQueries(IContextGetItemByPredicateRepository<ModelStepRecord, AgentBridgeContextKey> reader)
{
    /// <summary>Читает шаги конкретного обращения с обоими родителями локального ID.</summary>
    public Task<List<ModelStepRecord>> ReadTurnAsync(Guid dialogId, Guid turnId, CancellationToken cancellationToken = default) =>
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId && record.TurnId == turnId,
            asNoTracking: true, include: query => query.OrderBy(record => record.Sequence), ct: cancellationToken);

    /// <summary>Находит шаг только внутри заданного диалога и обращения.</summary>
    public Task<ModelStepRecord?> FindAsync(Guid dialogId, Guid turnId, Guid stepId, CancellationToken cancellationToken = default) =>
        reader.GetItemByPredicateAsync(record => record.DialogId == dialogId && record.TurnId == turnId && record.Id == stepId,
            asNoTracking: true, ct: cancellationToken);

    /// <summary>Читает шаги диалога одним base-запросом; порядок внутри обращения — Sequence выполнения.</summary>
    public Task<List<ModelStepRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.TurnId).ThenBy(record => record.Sequence), ct: cancellationToken);
}
