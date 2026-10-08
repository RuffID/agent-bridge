#if AGENTBRIDGE_SQLSERVER
using AgentBridge.Persistence.SqlServer;
#elif AGENTBRIDGE_SQLITE
using AgentBridge.Persistence.Sqlite;
#elif AGENTBRIDGE_POSTGRESQL
using AgentBridge.Persistence.PostgreSql;
#endif
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.RuntimeProbe;

/// <summary>Compile-only выбор модуля из поставленного комплекта без reflection или загрузки соседних провайдеров.</summary>
internal static class SelectedProviderRegistration
{
    /// <summary>Регистрирует ровно модуль выбранного при сборке комплекта; другие аргументы отклоняются.</summary>
    internal static IServiceCollection AddSelectedProvider(this IServiceCollection services, string provider)
    {
#if AGENTBRIDGE_SQLSERVER
        if (provider != "SqlServer") throw new ArgumentException("Kit provider mismatch.", nameof(provider));
        return services.AddAgentBridgeSqlServer();
#elif AGENTBRIDGE_SQLITE
        if (provider != "Sqlite") throw new ArgumentException("Kit provider mismatch.", nameof(provider));
        return services.AddAgentBridgeSqlite();
#elif AGENTBRIDGE_POSTGRESQL
        if (provider != "PostgreSql") throw new ArgumentException("Kit provider mismatch.", nameof(provider));
        return services.AddAgentBridgePostgreSql();
#else
        throw new InvalidOperationException("Compile with an explicit provider kit.");
#endif
    }
}
