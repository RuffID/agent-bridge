namespace AgentBridge.Application.Models;

/// <summary>Проверенный безопасный выбор модели и входных лимитов, без API-ключей и подключений.</summary>
public class ModelSettingsSnapshot
{
    /// <summary>Фиксирует проверенный выбор; создание выполняется после ModelSelectionValidator.</summary>
    public ModelSettingsSnapshot(ModelCapabilities model, string effort, int tokenThreshold, int inputTokenReserve)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);
        Model = model;
        ReasoningEffort = effort;
        TokenThreshold = tokenThreshold;
        InputTokenReserve = inputTokenReserve;
    }

    /// <summary>Выбранная модель с объявленными возможностями.</summary>
    public ModelCapabilities Model { get; }
    /// <summary>Проверенное точное усилие.</summary>
    public string ReasoningEffort { get; }
    /// <summary>Порог будущего сжатия; не измерение подготовленного запроса.</summary>
    public int TokenThreshold { get; }
    /// <summary>Запас входного бюджета.</summary>
    public int InputTokenReserve { get; }
}
