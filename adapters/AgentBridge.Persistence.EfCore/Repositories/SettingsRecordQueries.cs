using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает per-dialog выбор базовым CRUD; права/срок проверяет сценарий.</summary>
public class SettingsRecordQueries(IContextGetItemByIdRepository<DialogSettingsRecord, Guid, AgentBridgeContextKey> byId)
{
    private readonly HistoryQueryGuard? _history;
    private readonly IContextQueryRepository<DialogSettingsRecord, AgentBridgeContextKey>? _query;

    /// <summary>Ограничивает полные model/effort metadata до materialization.</summary>
    public SettingsRecordQueries(IContextGetItemByIdRepository<DialogSettingsRecord, Guid, AgentBridgeContextKey> byId,
        HistoryQueryGuard history, IContextQueryRepository<DialogSettingsRecord, AgentBridgeContextKey> query) : this(byId)
    {
        _history = history;
        _query = query;
    }

    /// <summary>Tracking только внутри write scope; ID совпадает с DialogId.</summary>
    public async Task<DialogSettingsRecord?> FindAsync(Guid id, CancellationToken ct, bool trackChanges = false)
    {
        if (_history is not null)
        {
            List<DialogSettingsRecord> rows = await _history.ReadAsync(_query!.Query(!trackChanges)
                .Where(record => record.Id == id), ct, take: 1);
            return rows.SingleOrDefault();
        }

        return await byId.GetItemByIdAsync(id, asNoTracking: !trackChanges, ct: ct);
    }
}
