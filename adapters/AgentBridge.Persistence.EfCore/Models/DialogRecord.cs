using EFCoreLibrary.Abstractions.Entity;

namespace AgentBridge.Persistence.EfCore.Models;

/// <inheritdoc/>
/// <remarks>Строка диалога; изменяемый DTO хранения, не доменный агрегат и не механизм авторизации.</remarks>
public class DialogRecord : IEntity<Guid>
{
    /// <summary>Явная catalog registration; legacy roots не индексируются автоматически.</summary>
    public bool CatalogRegistered { get; set; }
    /// <summary>Bounded persisted run/fencing state; concurrency token, не история.</summary>
    public string? RuntimeJson { get; set; }
    /// <inheritdoc/>
    public Guid Id { get; set; }
    /// <summary>Сохраняемая идентичность жизни; новая жизнь того же ID получает новый GUID при создании.</summary>
    public Guid IncarnationId { get; set; }
    /// <summary>Ожидаемая версия для optimistic concurrency, увеличиваемая сценарием записи.</summary>
    public long Revision { get; set; }
    /// <summary>Исходный ordinal ID владельца без нормализации.</summary>
    public string OwnerId { get; set; } = string.Empty;
    /// <summary>Фиксированное время создания в UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>Legacy metadata срока при создании; MaxValue кодирует бессрочный режим. Текущий срок вычисляется по политике и CreatedAtUtc.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
    /// <summary>Последнее изменение для проверки хронологии при восстановлении.</summary>
    public DateTimeOffset LastChangedAtUtc { get; set; }
    /// <summary>Байты сохраняемого содержимого, без overhead БД; подсчёт выполняет сценарий.</summary>
    public long ContentBytes { get; set; }
}
