using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;

namespace AgentBridge.Application;

/// <summary>Проверяет выбор и настроенный входной бюджет по конкретному динамическому каталогу.</summary>
public static class ModelSelectionValidator
{
    /// <summary>Не подменяет модель/effort и не считает токены фактического запроса.</summary>
    public static ServiceResult<ModelSettingsSnapshot> Validate(ModelCatalogSnapshot catalog,
        string model, string effort, int tokenThreshold, int inputTokenReserve)
    {
        return Validate(catalog, model, effort, tokenThreshold, inputTokenReserve, ContextBudgetPolicy.RequireLocalEstimate);
    }

    /// <summary>Неизвестное окно разрешено только отдельной явной политикой; effort и API-доступность обязательны.</summary>
    public static ServiceResult<ModelSettingsSnapshot> Validate(ModelCatalogSnapshot catalog,
        string model, string effort, int tokenThreshold, int inputTokenReserve, ContextBudgetPolicy budgetPolicy)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(effort)
            || tokenThreshold <= 0 || inputTokenReserve < 0 || !Enum.IsDefined(budgetPolicy))
        {
            return Fail(ServiceErrorType.Validation, "Некорректные модельные настройки.");
        }

        ModelCapabilities? selected = catalog.Models.SingleOrDefault(item => item.Id == model);
        if (selected is null)
        {
            return Fail(ServiceErrorType.Unsupported, "Выбранная модель отсутствует в доступном каталоге.");
        }

        bool unknownWindowAllowed = selected.InputContextWindow is null
            && budgetPolicy == ContextBudgetPolicy.ServerValidationWithUnknownWindow;
        if (!selected.HasMetadata || !selected.SupportedInApi
            || (!unknownWindowAllowed && selected.InputContextWindow is null or <= 0))
        {
            return Fail(ServiceErrorType.Unsupported, "Каталог не подтверждает доступность и входной бюджет модели.");
        }

        if (!selected.ReasoningEfforts.Contains(effort, StringComparer.Ordinal))
        {
            return Fail(ServiceErrorType.Unsupported, "Выбранное усилие не поддерживается моделью.");
        }

        if (selected.InputContextWindow is int window && (long)tokenThreshold + inputTokenReserve > window)
        {
            return Fail(ServiceErrorType.Validation, "Порог с запасом превышает объявленный входной бюджет модели.");
        }

        return ServiceResult<ModelSettingsSnapshot>.Ok(new(selected, effort, tokenThreshold, inputTokenReserve, budgetPolicy));
    }

    /// <summary>Создаёт безопасный отказ без данных.</summary>
    private static ServiceResult<ModelSettingsSnapshot> Fail(ServiceErrorType type, string message) =>
        ServiceResult<ModelSettingsSnapshot>.Fail(new(type, message));
}
