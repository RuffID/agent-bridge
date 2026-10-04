namespace AgentBridge.Application.Models;

/// <summary>Сохраняемая достоверность попытки; Started после restart запрещает повтор действия.</summary>
public enum ToolAttemptState
{
    /// <summary>Начало подтверждено до handler, итог ещё не сохранён.</summary>
    Started,
    /// <summary>Подтверждённый результат и canonical output сохранены атомарно.</summary>
    Succeeded,
    /// <summary>Подтверждённый отказ и canonical error output сохранены атомарно.</summary>
    Rejected,
    /// <summary>Действие началось, достоверного результата нет.</summary>
    Unknown,
    /// <summary>Handler не начинался; автоматический replay turn всё равно запрещён.</summary>
    NotStarted
}
