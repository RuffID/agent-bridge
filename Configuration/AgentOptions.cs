namespace AgentBridge.Configuration;

/// <summary>Настройки агента, предоставленные подключающим приложением.</summary>
public class AgentOptions
{
    /// <summary>Явно выбранный источник инструкций; отсутствие режима недопустимо.</summary>
    public AgentInstructionsSource? InstructionsSource { get; set; }

    /// <summary>Обязательные инструкции для режима Configuration; в режиме PerRequest задаются в обращении.</summary>
    public string? Instructions { get; set; }

    /// <summary>Максимальное число шагов инструментов на одно обращение.</summary>
    public int MaxToolSteps { get; set; }
}
