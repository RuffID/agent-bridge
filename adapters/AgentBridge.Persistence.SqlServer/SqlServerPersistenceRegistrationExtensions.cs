using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.SqlServer;

/// <summary>Явное подключение SqlServer без регистрации других провайдеров, подключения к БД или запуска обслуживания.</summary>
public static class SqlServerPersistenceRegistrationExtensions
{
    /// <summary>Регистрирует модуль SqlServer; Database.Provider должен выбрать его перед созданием контекста.</summary>
    public static IServiceCollection AddAgentBridgeSqlServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAgentBridgeDatabaseProvider<SqlServerDatabaseProvider>();

        return services;
    }
}
