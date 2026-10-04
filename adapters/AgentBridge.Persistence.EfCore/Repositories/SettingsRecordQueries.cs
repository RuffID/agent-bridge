using AgentBridge.Persistence.EfCore.Models;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает per-dialog выбор базовым CRUD; права/срок проверяет сценарий.</summary>
public class SettingsRecordQueries(IContextGetItemByIdRepository<DialogSettingsRecord, Guid, AgentBridgeContextKey> byId)
{
    /// <summary>Tracking только внутри write scope; ID совпадает с DialogId.</summary>
    public Task<DialogSettingsRecord?> FindAsync(Guid id, CancellationToken ct, bool trackChanges = false) =>
        byId.GetItemByIdAsync(id, asNoTracking: !trackChanges, ct: ct);
}
