namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Локально поддерживаемые варианты настройки будущего EF-хранилища.</summary>
public enum DatabaseProvider
{
    /// <summary>SQLite, только при явном выборе приложения.</summary>
    SQLite = 0,

    /// <summary>PostgreSQL, только при явном выборе приложения.</summary>
    PostgreSql = 1,

    /// <summary>Microsoft SQL Server, только при явном выборе приложения.</summary>
    SqlServer = 2
}
