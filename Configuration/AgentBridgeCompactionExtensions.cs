using AgentBridge.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Configuration;

/// <summary>Подключает явный сценарий compact без запуска операций и выбора источников контекста.</summary>
public static class AgentBridgeCompactionExtensions
{
    /// <summary>Приложение регистрирует ContextBuilder с выбранными providers, counter, gateway, writer и options.</summary>
    public static IServiceCollection AddAgentBridgeCompaction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ContextCompactor>();
        return services;
    }
}
