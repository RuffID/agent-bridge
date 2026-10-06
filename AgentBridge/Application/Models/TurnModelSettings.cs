namespace AgentBridge.Application.Models;

/// <summary>Безопасные primitive настройки, атомарно зафиксированные при начале обращения.</summary>
public class TurnModelSettings
{
    /// <summary>Фиксирует выбранную модель/effort и проверенные лимиты, без ключа или canonical содержимого.</summary>
    public TurnModelSettings(string model, string effort, int tokenThreshold, int inputTokenReserve, int inputContextWindow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);
        if (tokenThreshold <= 0 || inputTokenReserve < 0 || (long)tokenThreshold + inputTokenReserve > inputContextWindow)
            throw new ArgumentOutOfRangeException(nameof(tokenThreshold));
        Model = model;
        Effort = effort;
        TokenThreshold = tokenThreshold;
        InputTokenReserve = inputTokenReserve;
        InputContextWindow = inputContextWindow;
    }
    /// <summary>Выбранная модель обращения.</summary>
    public string Model { get; }
    /// <summary>Итоговый effort обращения, включая override.</summary>
    public string Effort { get; }
    /// <summary>Порог сжатия.</summary>
    public int TokenThreshold { get; }
    /// <summary>Запас входного бюджета.</summary>
    public int InputTokenReserve { get; }
    /// <summary>Объявленное входное окно, не context_window.</summary>
    public int InputContextWindow { get; }
    /// <summary>Копирует только безопасные primitive значения проверенного snapshot.</summary>
    public static TurnModelSettings From(ModelSettingsSnapshot settings) => new(settings.Model.Id, settings.ReasoningEffort,
        settings.TokenThreshold, settings.InputTokenReserve, settings.Model.InputContextWindow
            ?? throw new InvalidOperationException("Проверенное входное окно обязательно."));
}
