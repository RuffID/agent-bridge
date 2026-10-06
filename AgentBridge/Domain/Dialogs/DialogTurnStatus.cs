namespace AgentBridge.Domain.Dialogs;

/// <summary>Состояние обращения; частичный ответ не подтверждает успешное завершение.</summary>
public enum DialogTurnStatus
{
    /// <summary>Обращение выполняется.</summary>
    InProgress,
    /// <summary>Получено подтверждённое полное завершение.</summary>
    Completed,
    /// <summary>Обращение завершилось ошибкой.</summary>
    Failed,
    /// <summary>Обращение отменено вызывающим кодом.</summary>
    Canceled,
    /// <summary>Обращение закончено без подтверждённого полного результата.</summary>
    Incomplete
}
