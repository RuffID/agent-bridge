namespace AgentBridge.Application.Models;

/// <summary>Независимые объявленные возможности модели, без wire JSON и гарантий конкретного upstream.</summary>
public class ModelCapabilities
{
    /// <summary>Копирует списки; null metadata сохраняется явно через HasMetadata, неизвестные бюджеты не подменяются.</summary>
    public ModelCapabilities(string id, bool hasMetadata, int? contextWindow, int? inputContextWindow,
        int? maxOutputTokens, IEnumerable<string> reasoningEfforts, string? defaultReasoningEffort,
        IEnumerable<string> inputModalities, bool supportedInApi, bool supportsReasoningSummaries,
        bool supportsParallelToolCalls, bool supportsVerbosity, bool preferWebSockets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(reasoningEfforts);
        ArgumentNullException.ThrowIfNull(inputModalities);
        Id = id;
        HasMetadata = hasMetadata;
        ContextWindow = contextWindow;
        InputContextWindow = inputContextWindow;
        MaxOutputTokens = maxOutputTokens;
        ReasoningEfforts = Array.AsReadOnly(reasoningEfforts.ToArray());
        DefaultReasoningEffort = defaultReasoningEffort;
        InputModalities = Array.AsReadOnly(inputModalities.ToArray());
        SupportedInApi = supportedInApi;
        SupportsReasoningSummaries = supportsReasoningSummaries;
        SupportsParallelToolCalls = supportsParallelToolCalls;
        SupportsVerbosity = supportsVerbosity;
        PreferWebSockets = preferWebSockets;
    }

    /// <summary>Точный ID каталога, без alias normalization.</summary>
    public string Id { get; }
    /// <summary>Сервер предоставил metadata.</summary>
    public bool HasMetadata { get; }
    /// <summary>Объявленное окно контекста; не заменяет неизвестный входной бюджет.</summary>
    public int? ContextWindow { get; }
    /// <summary>Объявленный доступный входной бюджет, без локального подсчёта opaque-состояния.</summary>
    public int? InputContextWindow { get; }
    /// <summary>Объявленный максимальный выход, если сервер его предоставил.</summary>
    public int? MaxOutputTokens { get; }
    /// <summary>Точные поддержанные значения effort.</summary>
    public IReadOnlyList<string> ReasoningEfforts { get; }
    /// <summary>Объявленный default; не используется для скрытой замены.</summary>
    public string? DefaultReasoningEffort { get; }
    /// <summary>Объявленные входные модальности.</summary>
    public IReadOnlyList<string> InputModalities { get; }
    /// <summary>Модель объявлена доступной в API; не гарантирует поддержку Responses или compact.</summary>
    public bool SupportedInApi { get; }
    /// <summary>Объявленная поддержка сводок reasoning.</summary>
    public bool SupportsReasoningSummaries { get; }
    /// <summary>Объявленная поддержка параллельных вызовов инструментов.</summary>
    public bool SupportsParallelToolCalls { get; }
    /// <summary>Объявленная поддержка verbosity.</summary>
    public bool SupportsVerbosity { get; }
    /// <summary>Объявленное предпочтение WebSocket, без реализации этого транспорта.</summary>
    public bool PreferWebSockets { get; }
}
