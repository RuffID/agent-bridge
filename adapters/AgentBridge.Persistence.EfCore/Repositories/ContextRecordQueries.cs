using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает принятые версии compact без изменения или удаления предыдущих состояний.</summary>
public class ContextRecordQueries(IContextGetItemByPredicateRepository<DialogContextRecord, AgentBridgeContextKey> reader)
{
    /// <summary>Читает активную максимальную Version через сортировку base predicate.</summary>
    public Task<DialogContextRecord?> ReadActiveAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        reader.GetItemByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderByDescending(record => record.Version), ct: cancellationToken);

    /// <summary>Читает все сохранённые версии по возрастанию, не только активную.</summary>
    public Task<List<DialogContextRecord>> ReadAsync(Guid dialogId, CancellationToken cancellationToken = default) =>
        reader.GetItemsByPredicateAsync(record => record.DialogId == dialogId, asNoTracking: true,
            include: query => query.OrderBy(record => record.Version), ct: cancellationToken);
}
