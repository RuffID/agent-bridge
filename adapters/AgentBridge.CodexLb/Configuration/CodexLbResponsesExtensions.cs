using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Responses;
using HttpClientLibrary.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.CodexLb.Configuration;

/// <summary>Явно подключает JSON шлюз и существующий каталог через общий scoped HttpClientLibrary pipeline.</summary>
public static class CodexLbResponsesExtensions
{
    /// <summary>Регистрирует JSON IModelGateway, каталог и resolver; не запускает HTTP, host, SSE или compact.</summary>
    /// <remarks>Приложение предоставляет HttpClient без retry/смены аккаунта, logging/options и IIndividualModelKeySource; владеет HttpClient и handlers.</remarks>
    public static IServiceCollection AddCodexLbResponses(this IServiceCollection services,
        Func<IServiceProvider, HttpClient> httpClientFactory, HttpClientLoggingOptions? loggingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddCodexLbModelCatalog(httpClientFactory, loggingOptions);
        services.AddScoped<IModelGateway, CodexLbModelGateway>();
        return services;
    }
}
