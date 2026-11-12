namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Устойчивые имена сборок схемы общего контекста AgentBridge.</summary>
public static class AgentBridgeMigrationsAssemblies
{
    /// <summary>Сборка миграций PostgreSQL, поставляемая приложением для обслуживания.</summary>
    public const string POSTGRESQL = "AgentBridge.Persistence.Migrations.PostgreSql";

    /// <summary>Отдельная сборка миграций Microsoft SQL Server.</summary>
    public const string SQLSERVER = "AgentBridge.Persistence.Migrations.SqlServer";
}
