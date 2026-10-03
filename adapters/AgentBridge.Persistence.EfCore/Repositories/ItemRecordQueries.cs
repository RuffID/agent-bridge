using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает полные канонические items с фильтром диалога/обращения и сохранённым порядком.</summary>
public class ItemRecordQueries(IContextGetItemByPredicateRepository<CanonicalItemRecord, AgentBridgeContextKey> reader)
{
    /// <summary>Читает одно обращение, включая результаты инструментов и неизвестные поля.</summary>
    public Task<List<CanonicalItemRecord>> ReadTurnAsync(Guid dialogId, Guid turnId, CancellationToken cancellationToken = default) =>
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId && record.TurnId == turnId,
            asNoTracking: true, include: query => query.OrderBy(record => record.Sequence), ct: cancellationToken);

    /// <summary>Читает всю историю диалога одним base-запросом; порядок внутри каждого обращения — Sequence.</summary>
    public Task<List<CanonicalItemRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.TurnId).ThenBy(record => record.Sequence), ct: cancellationToken);
}
