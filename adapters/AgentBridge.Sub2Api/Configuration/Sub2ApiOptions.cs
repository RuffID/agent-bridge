namespace AgentBridge.Sub2Api.Configuration;

/// <summary>Собственные настройки sub2api; содержат секрет и не являются безопасным DTO.</summary>
public class Sub2ApiOptions
{
    /// <summary>Абсолютный server root/prefix, возможно с единственным завершающим /v1.</summary>
    public string? BaseAddress { get; set; }

    /// <summary>Точное имя выбранной модели; доступность ещё требует серверного каталога.</summary>
    public string? Model { get; set; }

    /// <summary>Точный effort приложения без выдуманного списка возможностей.</summary>
    public string ReasoningEffort { get; set; } = string.Empty;

    /// <summary>Явный режим gateway-ключа.</summary>
    public Sub2ApiKeySourceMode? KeySource { get; set; }

    /// <summary>Общий gateway-ключ; не OpenAI upstream credential.</summary>
    public string? SharedApiKey { get; set; }

    /// <summary>Конечный срок HTTP-операции; используется также для чтения каталога и optional usage.</summary>
    public TimeSpan GenerationTimeout { get; set; }

    /// <summary>Конечный срок compact.</summary>
    public TimeSpan CompactTimeout { get; set; }
}
