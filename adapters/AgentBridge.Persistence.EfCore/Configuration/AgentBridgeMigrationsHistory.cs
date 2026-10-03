namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Изолированная служебная история EF для общего контекста AgentBridge.</summary>
public static class AgentBridgeMigrationsHistory
{
    /// <summary>Общее для runtime и design-time имя; не смешивает историю с контекстом подключающего приложения.</summary>
    public const string TABLE_NAME = "__AgentBridgeMigrationsHistory";
}
