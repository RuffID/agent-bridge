namespace AgentBridge.CodexLb.Configuration;

/// <summary>Настройки подключения к codex-lb; не являются безопасным снимком настроек.</summary>
public class CodexLbOptions
{
    /// <summary>Абсолютный HTTP(S)-адрес сервера без учётных данных, query и fragment.</summary>
    public string? BaseAddress { get; set; }

    /// <summary>Модель приложения; обязательна, её доступность проверяется по серверному каталогу отдельно.</summary>
    public string? Model { get; set; }

    /// <summary>Effort по умолчанию; поддержка моделью и ключом проверяется отдельно, без скрытой замены.</summary>
    public string ReasoningEffort { get; set; } = "medium";

    /// <summary>Общий секретный ключ приложения; может отсутствовать при индивидуальных ключах.</summary>
    public string? SharedApiKey { get; set; }

    /// <summary>Конечное время ожидания генерации.</summary>
    public TimeSpan GenerationTimeout { get; set; } = TimeSpan.FromSeconds(180);

    /// <summary>Конечное время ожидания compact.</summary>
    public TimeSpan CompactTimeout { get; set; } = TimeSpan.FromSeconds(180);
}
