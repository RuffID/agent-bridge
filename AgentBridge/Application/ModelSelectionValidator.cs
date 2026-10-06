using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <summary>Проверяет выбор и настроенный входной бюджет по конкретному динамическому каталогу.</summary>
public static class ModelSelectionValidator
{
    /// <summary>Не подменяет модель/effort и не считает токены фактического запроса.</summary>
    public static ServiceResult<ModelSettingsSnapshot> Validate(ModelCatalogSnapshot catalog,
        string model, string effort, int tokenThreshold, int inputTokenReserve)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(effort)
            || tokenThreshold <= 0 || inputTokenReserve < 0)
        {
            return Fail(ServiceErrorType.Validation, "Некорректные модельные настройки.");
        }
        ModelCapabilities? selected = catalog.Models.SingleOrDefault(item => item.Id == model);
        if (selected is null)
        {
            return Fail(ServiceErrorType.Unsupported, "Выбранная модель отсутствует в доступном каталоге.");
        }
        if (!selected.HasMetadata || !selected.SupportedInApi || selected.InputContextWindow is null or <= 0)
        {
            return Fail(ServiceErrorType.Unsupported, "Каталог не подтверждает доступность и входной бюджет модели.");
        }
        if (!selected.ReasoningEfforts.Contains(effort, StringComparer.Ordinal))
        {
            return Fail(ServiceErrorType.Unsupported, "Выбранное усилие не поддерживается моделью.");
        }
        if ((long)tokenThreshold + inputTokenReserve > selected.InputContextWindow.Value)
        {
            return Fail(ServiceErrorType.Validation, "Порог с запасом превышает объявленный входной бюджет модели.");
        }
        return ServiceResult<ModelSettingsSnapshot>.Ok(new(selected, effort, tokenThreshold, inputTokenReserve));
    }

    /// <summary>Создаёт безопасный отказ без данных.</summary>
    private static ServiceResult<ModelSettingsSnapshot> Fail(ServiceErrorType type, string message) =>
        ServiceResult<ModelSettingsSnapshot>.Fail(new(type, message));
}
