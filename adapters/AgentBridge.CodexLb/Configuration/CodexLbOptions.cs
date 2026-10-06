namespace AgentBridge.CodexLb.Configuration;

/// <summary>Настройки подключения к codex-lb; не являются безопасным снимком настроек.</summary>
public class CodexLbOptions
{
    /// <summary>Абсолютный HTTP(S)-адрес сервера без учётных данных, query и fragment.</summary>
    public string? BaseAddress { get; set; }

    /// <summary>Модель приложения; обязательна, её доступность проверяется по серверному каталогу отдельно.</summary>
    public string? Model { get; set; }

    /// <summary>Обязательный effort приложения; поддержка моделью и ключом проверяется отдельно, без скрытой замены.</summary>
    public string ReasoningEffort { get; set; } = string.Empty;

    /// <summary>Явный режим ключа: Shared требует общий ключ, Individual запрещает общий fallback.</summary>
    public ModelKeySourceMode? KeySource { get; set; }

    /// <summary>Общий секретный ключ приложения; обязателен в Shared, не используется как fallback в Individual.</summary>
    public string? SharedApiKey { get; set; }

    /// <summary>Конечное время ожидания генерации.</summary>
    public TimeSpan GenerationTimeout { get; set; }

    /// <summary>Конечное время ожидания compact.</summary>
    public TimeSpan CompactTimeout { get; set; }
}
