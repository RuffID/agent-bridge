namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Поддерживаемые провайдеры EF-хранилища AgentBridge.</summary>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL, только при явном выборе приложения; числовая identity сохранена.</summary>
    PostgreSql = 1,

    /// <summary>Microsoft SQL Server, только при явном выборе приложения; числовая identity сохранена.</summary>
    SqlServer = 2
}
