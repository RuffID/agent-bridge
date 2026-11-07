using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;

namespace AgentBridge.Application;

/// <summary>Проверяет полный подготовленный input по каталогу, резерву и порогу, без отправки или сжатия.</summary>
public class ContextBudgetGuard
{
    private readonly IContextTokenCounter counter;
    private readonly ContextBudgetPolicy policy;

    /// <summary>Принимает единственный счётчик; сеть, хранение и часы не используются.</summary>
    public ContextBudgetGuard(IContextTokenCounter counter) : this(counter, ContextBudgetPolicy.RequireLocalEstimate) { }

    /// <summary>Принимает явную политику; ServerValidation не гарантирует серверный приём.</summary>
    public ContextBudgetGuard(IContextTokenCounter counter, ContextBudgetPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(counter);
        this.counter = counter;
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));

        this.policy = policy;
    }

    /// <summary>Проверяет локальный бюджет; явный ServerValidation делегирует неизвестную opaque часть серверу.</summary>
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
            settings.TokenThreshold, settings.InputTokenReserve, settings.BudgetPolicy);
        if (!selection.Success)
        {
            return ServiceResult<ContextBudgetAssessment>.Fail(selection.Error
                ?? throw new InvalidOperationException("Отказ выбора должен содержать ошибку."));
        }

        if (settings.Model.InputContextWindow is null && policy != ContextBudgetPolicy.ServerValidationWithUnknownWindow)
        {
            return Fail(ServiceErrorType.Unsupported, "Неизвестное входное окно требует явной серверной проверки.");
        }

        ServiceResult<ContextTokenCount> counted = await counter.CountAsync(request, cancellationToken);
        if (!counted.Success)
        {
            return ServiceResult<ContextBudgetAssessment>.Fail(counted.Error
                ?? throw new InvalidOperationException("Отказ подсчёта должен содержать ошибку."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ContextTokenCount count = counted.Data ?? throw new InvalidOperationException("Подсчёт должен содержать данные.");
        if (count.IsApproximateEncoding && policy == ContextBudgetPolicy.RequireLocalEstimate)
        {
            return Fail(ServiceErrorType.Unsupported, "Оценочная кодировка модели требует явной серверной проверки бюджета.");
        }

        if (settings.Model.InputContextWindow is null)
        {
            if (count.EstimatedInputTokens is null && !count.HasOpaqueContent)
            {
                return Fail(ServiceErrorType.Unsupported, "Полная оценка входного бюджета недоступна.");
            }

            bool reached = (count.EstimatedInputTokens ?? count.KnownTokens) >= settings.TokenThreshold;
            return ServiceResult<ContextBudgetAssessment>.Ok(new(count, null, settings.InputTokenReserve,
                reached, requiresServerValidation: true));
        }

        if (count.EstimatedInputTokens is not long estimate)
        {
            if (policy != ContextBudgetPolicy.RequireLocalEstimate && count.HasOpaqueContent)
            {
                if (count.KnownTokens > (long)settings.Model.InputContextWindow!.Value - settings.InputTokenReserve)
                {
                    return Fail(ServiceErrorType.Rejected, "Известная часть входа с резервом превышает входной бюджет модели.");
                }

                return ServiceResult<ContextBudgetAssessment>.Ok(new(count, settings.Model.InputContextWindow.Value,
                    settings.InputTokenReserve, count.KnownTokens >= settings.TokenThreshold, requiresServerValidation: true));
            }

            return Fail(ServiceErrorType.Unsupported, "Полная оценка входного бюджета недоступна.");
        }

        int inputWindow = settings.Model.InputContextWindow
            ?? throw new InvalidOperationException("Проверенная модель должна содержать входной бюджет.");
        if (estimate > (long)inputWindow - settings.InputTokenReserve)
        {
            return Fail(ServiceErrorType.Rejected, "Оценка входа с резервом превышает входной бюджет модели.");
        }

        return ServiceResult<ContextBudgetAssessment>.Ok(new(count, inputWindow,
            settings.InputTokenReserve, estimate >= settings.TokenThreshold, requiresServerValidation: count.IsApproximateEncoding));
    }

    /// <summary>Создаёт безопасный отказ без исходного содержимого.</summary>
    private static ServiceResult<ContextBudgetAssessment> Fail(ServiceErrorType type, string message) =>
        ServiceResult<ContextBudgetAssessment>.Fail(new(type, message));
}
