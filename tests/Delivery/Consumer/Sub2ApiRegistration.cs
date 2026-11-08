using AgentBridge.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only выбор sub2api при прежнем прикладном API facade.</summary>
public static class Sub2ApiRegistration
{
    /// <summary>Приложение предварительно регистрирует logger и модуль БД; configuration содержит Sub2Api вместо CodexLb.</summary>
    public static IServiceCollection AddSub2ApiAgentBridge(this IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, HttpClient> httpClientFactory)
    {
        services.AddAgentBridgeSub2ApiConnector();

        return services.AddAgentBridge(configuration, httpClientFactory);
    }
}
