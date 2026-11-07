using System.Text.Json.Serialization;
using AgentBridge.Configuration;

namespace AgentBridge.Application.Models;

/// <summary>Безопасные primitive настройки, атомарно зафиксированные при начале обращения.</summary>
public class TurnModelSettings
{
    /// <summary>Фиксирует выбранную модель/effort и проверенные лимиты, без ключа или canonical содержимого.</summary>
    public TurnModelSettings(string model, string effort, int tokenThreshold, int inputTokenReserve, int inputContextWindow)
        : this(model, effort, tokenThreshold, inputTokenReserve, inputContextWindow, ContextBudgetPolicy.RequireLocalEstimate) { }

    /// <summary>Фиксирует неизвестное окно только вместе с явным разрешением серверной проверки.</summary>
    [JsonConstructor]
    public TurnModelSettings(string model, string effort, int tokenThreshold, int inputTokenReserve,
        int? declaredInputContextWindow, ContextBudgetPolicy budgetPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);
        if (tokenThreshold <= 0 || inputTokenReserve < 0 || !Enum.IsDefined(budgetPolicy)
            || declaredInputContextWindow is <= 0
            || declaredInputContextWindow is null && budgetPolicy != ContextBudgetPolicy.ServerValidationWithUnknownWindow
            || declaredInputContextWindow is int window && (long)tokenThreshold + inputTokenReserve > window)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenThreshold));
        }

        Model = model;
        Effort = effort;
        TokenThreshold = tokenThreshold;
        InputTokenReserve = inputTokenReserve;
        DeclaredInputContextWindow = declaredInputContextWindow;
        BudgetPolicy = budgetPolicy;
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
    /// <exception cref="InvalidOperationException">Окно неизвестно; использовать nullable DeclaredInputContextWindow.</exception>
    [JsonIgnore]
    public int InputContextWindow => DeclaredInputContextWindow
        ?? throw new InvalidOperationException("Входное окно неизвестно; числовой лимит недоступен.");
    /// <summary>Объявленное входное окно либо null, без guessed defaults.</summary>
    [JsonPropertyName(nameof(InputContextWindow))]
    public int? DeclaredInputContextWindow { get; }
    /// <summary>Явная политика snapshot без connector provenance.</summary>
    public ContextBudgetPolicy BudgetPolicy { get; }
    /// <summary>Копирует только безопасные primitive значения проверенного snapshot.</summary>
    public static TurnModelSettings From(ModelSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(settings.Model.Id, settings.ReasoningEffort, settings.TokenThreshold, settings.InputTokenReserve,
            settings.Model.InputContextWindow, settings.BudgetPolicy);
    }
}
