namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Поддерживаемый провайдер EF-хранилища AgentBridge.</summary>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL, только при явном выборе приложения; числовая identity сохранена.</summary>
    PostgreSql = 1
}
