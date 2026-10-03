namespace AgentBridge.Configuration;

/// <summary>Локальные лимиты рабочего контекста, без проверки каталога моделей.</summary>
public class ContextCompactionOptions
{
    /// <summary>Порог запуска сжатия в токенах подготовленного контекста.</summary>
    public int TokenThreshold { get; set; } = 32_000;

    /// <summary>Запас входного бюджета сверх подготовленного контекста в токенах.</summary>
    public int InputTokenReserve { get; set; } = 4_096;

    /// <summary>Максимальное число последовательных проходов сжатия на обращение.</summary>
    public int MaxPasses { get; set; } = 3;
}
