using AgentBridge.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only базовое подключение Shared без пустых business классов.</summary>
public static class SimpleRegistration
{
    /// <summary>Подключает facade после app logging и явного модуля БД; фабрику и срок владения HttpClient задаёт приложение.</summary>
    public static IServiceCollection AddSimpleAgentBridge(this IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, HttpClient> httpClientFactory) => services.AddAgentBridge(configuration, httpClientFactory);
}
