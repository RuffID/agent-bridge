using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Configuration;

/// <summary>Явное подключение safe settings/status; read/write/model/counter принадлежат приложению и адаптерам.</summary>
public static class AgentBridgeSettingsExtensions
{
    /// <summary>Регистрирует scoped services без I/O, сохраняя существующие регистрации приложения.</summary>
    public static IServiceCollection AddAgentBridgeSettings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ContextModelGuard>();
        services.TryAddScoped<AgentSettingsService>();
        services.TryAddSingleton<IContextContentInspector, ContextTokenCounter>();
        return services;
    }
}
