using AgentBridge.Configuration;

namespace AgentBridge.Application.Models;

/// <summary>Проверенный безопасный выбор модели и входных лимитов, без API-ключей и подключений.</summary>
public class ModelSettingsSnapshot
{
    /// <summary>Фиксирует проверенный выбор; создание выполняется после ModelSelectionValidator.</summary>
    public ModelSettingsSnapshot(ModelCapabilities model, string effort, int tokenThreshold, int inputTokenReserve)
        : this(model, effort, tokenThreshold, inputTokenReserve, ContextBudgetPolicy.RequireLocalEstimate) { }

    /// <summary>Фиксирует явную политику неизвестного окна отдельно от объявленных capabilities.</summary>
    public ModelSettingsSnapshot(ModelCapabilities model, string effort, int tokenThreshold, int inputTokenReserve,
        ContextBudgetPolicy budgetPolicy)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);
        if (!Enum.IsDefined(budgetPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(budgetPolicy));
        }

        Model = model;
        ReasoningEffort = effort;
        TokenThreshold = tokenThreshold;
        InputTokenReserve = inputTokenReserve;
        BudgetPolicy = budgetPolicy;
    }

    /// <summary>Выбранная модель с объявленными возможностями.</summary>
    public ModelCapabilities Model { get; }
    /// <summary>Проверенное точное усилие.</summary>
    public string ReasoningEffort { get; }
    /// <summary>Порог будущего сжатия; не измерение подготовленного запроса.</summary>
    public int TokenThreshold { get; }
    /// <summary>Запас входного бюджета.</summary>
    public int InputTokenReserve { get; }
    /// <summary>Разрешение неизвестного окна; не подтверждение серверных возможностей.</summary>
    public ContextBudgetPolicy BudgetPolicy { get; }
}
