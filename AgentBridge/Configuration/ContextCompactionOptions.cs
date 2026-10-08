namespace AgentBridge.Configuration;

/// <summary>Локальные лимиты рабочего контекста, без проверки каталога моделей.</summary>
public class ContextCompactionOptions
{
    /// <summary>По умолчанию требуется полная локальная оценка; ServerValidation включается приложением явно.</summary>
    public ContextBudgetPolicy BudgetPolicy { get; set; }

    /// <summary>Порог запуска сжатия в токенах подготовленного контекста.</summary>
    public int TokenThreshold { get; set; }

    /// <summary>Запас входного бюджета сверх подготовленного контекста в токенах.</summary>
    /// <remarks>Начальный -1 обозначает отсутствие настройки; явно заданный ноль допустим.</remarks>
    public int InputTokenReserve { get; set; } = -1;

    /// <summary>Максимальное число последовательных проходов сжатия на обращение.</summary>
    public int MaxPasses { get; set; }
}
