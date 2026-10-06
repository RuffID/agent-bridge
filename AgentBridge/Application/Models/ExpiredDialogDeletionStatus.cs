namespace AgentBridge.Application.Models;

/// <summary>Подтверждённость удаления отдельного кандидата без предположения о неизвестном commit.</summary>
public enum ExpiredDialogDeletionStatus
{
    /// <summary>Порт удаления не вызывался.</summary>
    NotAttempted,
    /// <summary>Порт подтвердил атомарное удаление.</summary>
    Deleted,
    /// <summary>Порт вернул ожидаемый отказ.</summary>
    Failed,
    /// <summary>Порт вызван, но подтверждённый исход не получен.</summary>
    Unknown
}
