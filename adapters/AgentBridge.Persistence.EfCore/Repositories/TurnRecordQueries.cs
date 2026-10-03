using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает обращения только с DialogId родителя, в порядке начала.</summary>
public class TurnRecordQueries(IContextGetItemByPredicateRepository<DialogTurnRecord, AgentBridgeContextKey> reader)
{
    /// <summary>Находит локальный ID обращения вместе с его диалогом.</summary>
    public Task<DialogTurnRecord?> FindAsync(Guid dialogId, Guid turnId, CancellationToken cancellationToken = default, bool trackChanges = false) =>
        reader.GetItemByPredicateAsync(record => record.DialogId == dialogId && record.Id == turnId,
            asNoTracking: !trackChanges, ct: cancellationToken);

    /// <summary>Читает всю историю обращений без отсечения по compact.</summary>
    public Task<List<DialogTurnRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.Sequence), ct: cancellationToken);
}
