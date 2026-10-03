namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Устойчивые имена отдельных сборок схемы для общего контекста AgentBridge.</summary>
public static class AgentBridgeMigrationsAssemblies
{
    /// <summary>Сборка миграций SQLite; приложение поставляет её для обслуживания выбранной БД.</summary>
    public const string SQLITE = "AgentBridge.Persistence.Migrations.Sqlite";

    /// <summary>Сборка миграций PostgreSQL; не используется при выборе SQLite.</summary>
    public const string POSTGRESQL = "AgentBridge.Persistence.Migrations.PostgreSql";
}
