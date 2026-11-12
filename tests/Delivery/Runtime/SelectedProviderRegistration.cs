#if AGENTBRIDGE_SQLSERVER
using AgentBridge.Persistence.SqlServer;
#elif AGENTBRIDGE_POSTGRESQL
using AgentBridge.Persistence.PostgreSql;
#endif
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.RuntimeProbe;

/// <summary>Регистрирует провайдер из явно выбранного комплекта.</summary>
internal static class SelectedProviderRegistration
{
    /// <summary>Отклоняет несовпадение провайдера комплекта без reflection или загрузки DLL.</summary>
    internal static IServiceCollection AddSelectedProvider(this IServiceCollection services, string provider)
    {
#if AGENTBRIDGE_SQLSERVER
        if (provider != "SqlServer")
        {
            throw new ArgumentException("Kit provider mismatch.", nameof(provider));
        }

        return services.AddAgentBridgeSqlServer();
#elif AGENTBRIDGE_POSTGRESQL
        if (provider != "PostgreSql")
        {
            throw new ArgumentException("Kit provider mismatch.", nameof(provider));
        }

        return services.AddAgentBridgePostgreSql();
#else
        throw new InvalidOperationException("Compile with an explicit provider kit.");
#endif
    }
}
