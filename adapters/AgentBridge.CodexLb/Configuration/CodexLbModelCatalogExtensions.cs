using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Models;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentBridge.CodexLb.Configuration;

/// <summary>Явно подключает каталог через HttpClientLibrary без HTTP при регистрации или разрешении сервисов.</summary>
public static class CodexLbModelCatalogExtensions
{
    /// <summary>Регистрирует scoped доступ, каталог и чтение модельных настроек.</summary>
    /// <remarks>Приложение регистрирует IIndividualModelKeySource, options и logging, владеет HttpClient, его handlers и временем ожидания. Фабрика не должна выполнять I/O.</remarks>
    public static IServiceCollection AddCodexLbModelCatalog(this IServiceCollection services,
        Func<IServiceProvider, HttpClient> httpClientFactory, HttpClientLoggingOptions? loggingOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        HttpErrorContentLogMode mode = loggingOptions?.ErrorContentMode ?? HttpErrorContentLogMode.None;
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(loggingOptions));
        }
        services.AddScoped(provider => new HttpApiClient(httpClientFactory(provider),
            provider.GetRequiredService<ILogger<HttpApiClient>>(), null,
            new HttpClientLoggingOptions { ErrorContentMode = mode }, new HttpErrorResponseOptions { MaxBodyBytes = 65_536 }));
        services.AddScoped<IModelAccessResolver, CodexLbModelAccessResolver>();
        services.AddScoped<IModelCatalog, CodexLbModelCatalog>();
        services.AddScoped<IModelSettingsReader, CodexLbModelSettingsReader>();
        return services;
    }
}
