using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <summary>Проверяет полный подготовленный input по каталогу, резерву и порогу, без отправки или сжатия.</summary>
public class ContextBudgetGuard
{
    private readonly IContextTokenCounter counter;

    /// <summary>Принимает единственный счётчик; сеть, хранение и часы не используются.</summary>
    public ContextBudgetGuard(IContextTokenCounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        this.counter = counter;
    }

    /// <summary>Отклоняет неизвестный полный бюджет; успешная локальная оценка не является server count.</summary>
    public async Task<ServiceResult<ContextBudgetAssessment>> CheckAsync(ModelRequest request,
        ModelSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Model != settings.Model.Id || request.ReasoningEffort != settings.ReasoningEffort)
        {
            return Fail(ServiceErrorType.Validation, "Запрос не соответствует проверенным модельным настройкам.");
        }
        ServiceResult<ModelSettingsSnapshot> selection = ModelSelectionValidator.Validate(
            new ModelCatalogSnapshot([settings.Model]), settings.Model.Id, settings.ReasoningEffort,
            settings.TokenThreshold, settings.InputTokenReserve);
        if (!selection.Success)
        {
            return ServiceResult<ContextBudgetAssessment>.Fail(selection.Error
                ?? throw new InvalidOperationException("Отказ выбора должен содержать ошибку."));
        }
        ServiceResult<ContextTokenCount> counted = await counter.CountAsync(request, cancellationToken);
        if (!counted.Success)
        {
            return ServiceResult<ContextBudgetAssessment>.Fail(counted.Error
                ?? throw new InvalidOperationException("Отказ подсчёта должен содержать ошибку."));
        }
        cancellationToken.ThrowIfCancellationRequested();
        ContextTokenCount count = counted.Data ?? throw new InvalidOperationException("Подсчёт должен содержать данные.");
        if (count.EstimatedInputTokens is not long estimate)
        {
            return Fail(ServiceErrorType.Unsupported, "Полная оценка входного бюджета недоступна.");
        }
        int inputWindow = settings.Model.InputContextWindow
            ?? throw new InvalidOperationException("Проверенная модель должна содержать входной бюджет.");
        if (estimate > (long)inputWindow - settings.InputTokenReserve)
        {
            return Fail(ServiceErrorType.Rejected, "Оценка входа с резервом превышает входной бюджет модели.");
        }
        return ServiceResult<ContextBudgetAssessment>.Ok(new(count, inputWindow,
            settings.InputTokenReserve, estimate >= settings.TokenThreshold));
    }

    /// <summary>Создаёт безопасный отказ без исходного содержимого.</summary>
    private static ServiceResult<ContextBudgetAssessment> Fail(ServiceErrorType type, string message) =>
        ServiceResult<ContextBudgetAssessment>.Fail(new(type, message));
}
