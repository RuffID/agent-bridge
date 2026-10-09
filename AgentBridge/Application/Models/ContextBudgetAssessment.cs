namespace AgentBridge.Application.Models;

/// <summary>Локальная оценка допустимого входа с отдельным резервом и признаком достижения порога.</summary>
public class ContextBudgetAssessment
{
    /// <summary>Фиксирует результат успешной проверки; не гарантирует серверный подсчёт или приём запроса.</summary>
    public ContextBudgetAssessment(ContextTokenCount tokenCount, int inputContextWindow, int inputTokenReserve,
        bool thresholdReached) : this(tokenCount, inputContextWindow, inputTokenReserve, thresholdReached, false) { }

    /// <summary>Явно отличает оценочную кодировку и неизвестный opaque бюджет от подтверждённого локального подсчёта.</summary>
    public ContextBudgetAssessment(ContextTokenCount tokenCount, int inputContextWindow, int inputTokenReserve,
        bool thresholdReached, bool requiresServerValidation)
    {
        ArgumentNullException.ThrowIfNull(tokenCount);
        if (inputContextWindow <= 0 || inputTokenReserve < 0 || inputTokenReserve > inputContextWindow)
        {
            throw new ArgumentOutOfRangeException(nameof(inputContextWindow), "Некорректный входной лимит или резерв.");
        }
        if ((!requiresServerValidation && (tokenCount.EstimatedInputTokens is null || tokenCount.IsApproximateEncoding))
            || requiresServerValidation && !tokenCount.IsApproximateEncoding
                && (!tokenCount.HasOpaqueContent || tokenCount.EstimatedInputTokens is not null)
            || tokenCount.EstimatedInputTokens is null && !tokenCount.HasOpaqueContent
            || tokenCount.KnownTokens > (long)inputContextWindow - inputTokenReserve
            || tokenCount.EstimatedInputTokens is long estimate && estimate > (long)inputContextWindow - inputTokenReserve)
        {
            throw new ArgumentException("Оценка должна соответствовать режиму проверки и входному бюджету.", nameof(tokenCount));
        }
        TokenCount = tokenCount;
        InputContextWindow = inputContextWindow;
        InputTokenReserve = inputTokenReserve;
        ThresholdReached = thresholdReached;
        RequiresServerValidation = requiresServerValidation;
    }

    /// <summary>Известная часть и локальная оценка входа до резерва.</summary>
    public ContextTokenCount TokenCount { get; }
    /// <summary>Входной лимит из каталога; не ContextWindow или MaxOutputTokens.</summary>
    public int InputContextWindow { get; }
    /// <summary>Отдельный настроенный запас входного бюджета.</summary>
    public int InputTokenReserve { get; }
    /// <summary>Оценка достигла настроенного порога; compact автоматически не запускается.</summary>
    public bool ThresholdReached { get; }

    /// <summary>Есть оценочная кодировка или неизвестная opaque часть; приём полного бюджета делегирован серверу.</summary>
    public bool RequiresServerValidation { get; }
}
