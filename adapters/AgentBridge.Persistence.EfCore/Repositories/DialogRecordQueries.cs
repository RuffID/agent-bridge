using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Configuration;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Читает строки диалога базовыми ID/predicate операциями; авторизацию выполняет прикладной сценарий.</summary>
public class DialogRecordQueries(
    IContextGetItemByIdRepository<DialogRecord, Guid, AgentBridgeContextKey> byId,
    IContextGetItemByPredicateRepository<DialogRecord, AgentBridgeContextKey> byPredicate,
    DialogRetentionPolicy retention)
{
    /// <summary>Читает глобальный ID; tracking допускается только внутри инфраструктуры сценария записи.</summary>
    public Task<DialogRecord?> FindAsync(Guid dialogId, CancellationToken cancellationToken = default, bool trackChanges = false) =>
        byId.GetItemByIdAsync(dialogId, asNoTracking: !trackChanges, ct: cancellationToken);

    /// <summary>Читает кандидатов по cutoff текущей политики в стабильном порядке создания и ID.</summary>
    public Task<List<DialogRecord>> ReadExpiredAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время должно быть UTC.", nameof(nowUtc));
        }
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset? createdBefore = retention.RetentionPeriod is { } period && period.Ticks <= nowUtc.UtcTicks
            ? nowUtc.Subtract(period) : null;
        return byPredicate.GetItemsByPredicateAsync(record => record.CatalogRegistered
                ? record.ExpiresAtUtc != DateTimeOffset.MaxValue && record.ExpiresAtUtc <= nowUtc
                : createdBefore != null && record.CreatedAtUtc <= createdBefore, take: limit,
            asNoTracking: true, include: query => query.OrderBy(record => record.CreatedAtUtc).ThenBy(record => record.Id),
            ct: cancellationToken);
    }
}
