using AgentBridge.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Configuration;

/// <summary>Явно подключает public run без исполнения модели или открытия БД.</summary>
public static class AgentBridgeRunnerExtensions
{
    /// <summary>Приложение предоставляет scoped settings/access/gateway и ContextBuilder с ordered providers.</summary>
    public static IServiceCollection AddAgentBridgeRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AgentRunner>();
        return services;
    }
}
