using AgentBridge.Domain.Dialogs;
using EFCoreLibrary.Abstractions.Entity;

namespace AgentBridge.Persistence.EfCore.Models;

/// <inheritdoc/>
/// <remarks>Строка обращения с идентичностью внутри диалога и стабильным порядком начала.</remarks>
public class DialogTurnRecord : IEntity<Guid>
{
    /// <inheritdoc/>
    public Guid Id { get; set; }
    /// <summary>Обязательный родитель.</summary>
    public Guid DialogId { get; set; }
    /// <summary>Порядок начала, начиная с единицы.</summary>
    public long Sequence { get; set; }
    /// <summary>Статус выполнения обращения.</summary>
    public DialogTurnStatus Status { get; set; }
    /// <summary>Время начала в UTC.</summary>
    public DateTimeOffset StartedAtUtc { get; set; }
    /// <summary>Время конечного состояния; отсутствует у InProgress.</summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }
}
