using AgentBridge.Persistence.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.RuntimeProbe;

/// <summary>Регистрирует PostgreSQL из явно выбранного комплекта.</summary>
internal static class SelectedProviderRegistration
{
    /// <summary>Отклоняет несовпадение провайдера комплекта без reflection или загрузки DLL.</summary>
    internal static IServiceCollection AddSelectedProvider(this IServiceCollection services, string provider)
    {
        if (provider != "PostgreSql")
        {
            throw new ArgumentException("Kit provider mismatch.", nameof(provider));
        }

        return services.AddAgentBridgePostgreSql();
    }
}
