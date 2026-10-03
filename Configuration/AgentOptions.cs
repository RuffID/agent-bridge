namespace AgentBridge.Configuration;

/// <summary>Настройки агента, предоставленные подключающим приложением.</summary>
public class AgentOptions
{
    /// <summary>Инструкции агента; отсутствие допускается, если приложение формирует их для обращения.</summary>
    public string? Instructions { get; set; }

    /// <summary>Максимальное число шагов инструментов на одно обращение.</summary>
    public int MaxToolSteps { get; set; } = 8;
}
