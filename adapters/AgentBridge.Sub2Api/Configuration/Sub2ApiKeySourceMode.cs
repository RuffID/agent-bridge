namespace AgentBridge.Sub2Api.Configuration;

/// <summary>Источник gateway-ключа sub2api, независимый от конфигурации codex-lb.</summary>
public enum Sub2ApiKeySourceMode
{
    /// <summary>Общий ключ допустим только при отсутствии индивидуального.</summary>
    Shared,
    /// <summary>Только ключ из источника приложения.</summary>
    Individual
}
