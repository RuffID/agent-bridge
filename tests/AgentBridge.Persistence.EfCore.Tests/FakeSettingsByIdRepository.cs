using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <inheritdoc/>
internal class FakeSettingsByIdRepository(FakeBaseRepository<DialogSettingsRecord> records) :
    IContextGetItemByIdRepository<DialogSettingsRecord, Guid, AgentBridgeContextKey>
{
    /// <inheritdoc/>
    public Task<DialogSettingsRecord?> GetItemByIdAsync(Guid id, bool asNoTracking = false,
        Func<IQueryable<DialogSettingsRecord>, IQueryable<DialogSettingsRecord>>? include = null, CancellationToken ct = default) =>
        records.GetItemByPredicateAsync(record => record.Id == id, asNoTracking, include, ct);
}
