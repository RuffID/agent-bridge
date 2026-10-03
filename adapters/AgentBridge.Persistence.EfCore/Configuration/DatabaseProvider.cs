namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Локально поддерживаемые варианты настройки будущего EF-хранилища.</summary>
public enum DatabaseProvider
{
    /// <summary>SQLite, только при явном выборе приложения.</summary>
    SQLite,

    /// <summary>PostgreSQL, только при явном выборе приложения.</summary>
    PostgreSql
}
