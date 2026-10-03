using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <inheritdoc/>
internal class FakeDialogByIdRepository(FakeBaseRepository<DialogRecord> records) :
    IContextGetItemByIdRepository<DialogRecord, Guid, AgentBridgeContextKey>
{
    /// <inheritdoc/>
    public Task<DialogRecord?> GetItemByIdAsync(Guid id, bool asNoTracking = false,
        Func<IQueryable<DialogRecord>, IQueryable<DialogRecord>>? include = null, CancellationToken ct = default) =>
        records.GetItemByPredicateAsync(record => record.Id == id, asNoTracking, include, ct);
}
