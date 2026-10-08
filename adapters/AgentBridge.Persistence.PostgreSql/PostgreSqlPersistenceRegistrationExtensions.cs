using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.PostgreSql;

/// <summary>Явное подключение PostgreSql без регистрации других провайдеров, подключения к БД или запуска обслуживания.</summary>
public static class PostgreSqlPersistenceRegistrationExtensions
{
    /// <summary>Регистрирует модуль PostgreSql; Database.Provider должен выбрать его перед созданием контекста.</summary>
    public static IServiceCollection AddAgentBridgePostgreSql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAgentBridgeDatabaseProvider<PostgreSqlDatabaseProvider>();

        return services;
    }
}
