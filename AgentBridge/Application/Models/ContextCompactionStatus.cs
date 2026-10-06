namespace AgentBridge.Application.Models;

/// <summary>Причина остановки ограниченного сценария; не является разрешением генерации.</summary>
public enum ContextCompactionStatus
{
    /// <summary>Порог не достигнут.</summary>
    NotRequired,
    /// <summary>Полный запрос стал меньше порога.</summary>
    TargetReached,
    /// <summary>Полная оценка неизвестна; дальнейшие проходы остановлены.</summary>
    UnknownBudget,
    /// <summary>Кандидат не уменьшает полную оценку и не принят.</summary>
    NoReduction,
    /// <summary>Достигнут предел числа проходов.</summary>
    PassLimitReached,
    /// <summary>Сохраняемая история пуста; transient вклад не сжимается.</summary>
    NoPersistableHistory,
    /// <summary>Ожидаемый отказ; последнее успешно принятое окно сохранено.</summary>
    Failed
}
