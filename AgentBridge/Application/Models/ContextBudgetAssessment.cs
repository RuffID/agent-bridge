using System.Text.Json.Serialization;

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
        : this(tokenCount, (int?)inputContextWindow, inputTokenReserve, thresholdReached, requiresServerValidation) { }

    /// <summary>Сохраняет null при неизвестном окне; такой результат всегда требует серверной проверки.</summary>
    public ContextBudgetAssessment(ContextTokenCount tokenCount, int? inputContextWindow, int inputTokenReserve,
        bool thresholdReached, bool requiresServerValidation)
    {
        ArgumentNullException.ThrowIfNull(tokenCount);
        if (inputContextWindow is <= 0 || inputTokenReserve < 0 || inputTokenReserve > inputContextWindow)
        {
            throw new ArgumentOutOfRangeException(nameof(inputContextWindow), "Некорректный входной лимит или резерв.");
        }

        bool invalidLocalCount = !requiresServerValidation
            && (tokenCount.EstimatedInputTokens is null || tokenCount.IsApproximateEncoding);
        bool unjustifiedKnownWindowDelegation = requiresServerValidation && inputContextWindow is not null
            && !tokenCount.IsApproximateEncoding && (!tokenCount.HasOpaqueContent || tokenCount.EstimatedInputTokens is not null);
        bool unknownNonOpaqueCount = tokenCount.EstimatedInputTokens is null && !tokenCount.HasOpaqueContent;
        bool unknownWindowWithoutDelegation = inputContextWindow is null && !requiresServerValidation;
        bool overKnownWindow = inputContextWindow is int window
            && (tokenCount.KnownTokens > (long)window - inputTokenReserve
                || tokenCount.EstimatedInputTokens is long estimate && estimate > (long)window - inputTokenReserve);
        if (invalidLocalCount || unjustifiedKnownWindowDelegation || unknownNonOpaqueCount
            || unknownWindowWithoutDelegation || overKnownWindow)
        {
            throw new ArgumentException("Оценка должна соответствовать режиму проверки и входному бюджету.", nameof(tokenCount));
        }

        TokenCount = tokenCount;
        DeclaredInputContextWindow = inputContextWindow;
        InputTokenReserve = inputTokenReserve;
        ThresholdReached = thresholdReached;
        RequiresServerValidation = requiresServerValidation;
    }

    /// <summary>Известная часть и локальная оценка входа до резерва.</summary>
    public ContextTokenCount TokenCount { get; }
    /// <summary>Входной лимит из каталога; не ContextWindow или MaxOutputTokens.</summary>
    /// <exception cref="InvalidOperationException">Окно неизвестно; использовать nullable DeclaredInputContextWindow.</exception>
    [JsonIgnore]
    public int InputContextWindow => DeclaredInputContextWindow
        ?? throw new InvalidOperationException("Входное окно неизвестно; числовой лимит недоступен.");
    /// <summary>Объявленное окно либо null; локальный порог не подставляется.</summary>
    [JsonPropertyName(nameof(InputContextWindow))]
    public int? DeclaredInputContextWindow { get; }
    /// <summary>Отдельный настроенный запас входного бюджета.</summary>
    public int InputTokenReserve { get; }
    /// <summary>Оценка достигла настроенного порога; compact автоматически не запускается.</summary>
    public bool ThresholdReached { get; }

    /// <summary>Есть оценочная кодировка или неизвестная opaque часть; приём полного бюджета делегирован серверу.</summary>
    public bool RequiresServerValidation { get; }
}
