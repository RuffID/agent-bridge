namespace AgentBridge.Configuration;

/// <summary>Источник обязательных инструкций агента, явно выбранный приложением.</summary>
public enum AgentInstructionsSource
{
    /// <summary>Инструкции заданы в AgentOptions; обращение может явно переопределить их.</summary>
    Configuration = 0,

    /// <summary>Каждое обращение предоставляет инструкции; значение options не служит fallback.</summary>
    PerRequest = 1
}
