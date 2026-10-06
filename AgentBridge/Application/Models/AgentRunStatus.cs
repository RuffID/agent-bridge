namespace AgentBridge.Application.Models;

/// <summary>Явный итог сценария, независимый от предварительных stream updates.</summary>
public enum AgentRunStatus
{
    /// <summary>Модель завершена и terminal save подтверждён.</summary>
    Completed,
    /// <summary>Явный отказ модели, guard или storage.</summary>
    Failed,
    /// <summary>Наблюдалась отмена caller.</summary>
    Canceled,
    /// <summary>Модель не подтвердила полное завершение.</summary>
    Incomplete,
    /// <summary>Попытка неизвестна или существующий turn запрещает автоматический replay.</summary>
    Interrupted
}
