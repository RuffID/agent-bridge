using AgentBridge.Application.Ports;
using AgentBridge.Configuration;
using AgentBridge.Sub2Api.Models;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AgentBridge.Sub2Api.Configuration;

/// <summary>Собирает existing bundle; HTTP создаётся только после локальной проверки запроса.</summary>
internal static class Sub2ApiComposition
{
    private const double MAX_TIMEOUT_MILLISECONDS = uint.MaxValue - 1d;

    /// <summary>Привязывает только Sub2Api и проверяет локальную конфигурацию без I/O.</summary>
    internal static void AddConfiguration(IServiceCollection services, IConfiguration configuration, bool hasIndividualSource)
    {
        services.AddOptions<Sub2ApiOptions>().BindSafely(configuration, "Sub2Api",
            nameof(Sub2ApiOptions.BaseAddress), nameof(Sub2ApiOptions.Model), nameof(Sub2ApiOptions.KeySource),
            nameof(Sub2ApiOptions.ReasoningEffort), nameof(Sub2ApiOptions.GenerationTimeout), nameof(Sub2ApiOptions.CompactTimeout))
            .Configure(options =>
            {
                if (options.KeySource == Sub2ApiKeySourceMode.Shared)
                {
                    SafeOptionsBindingExtensions.RequireValues<Sub2ApiOptions>(configuration, "Sub2Api", Options.DefaultName,
                        nameof(Sub2ApiOptions.SharedApiKey));
                }
            })
            .Validate(options => options.KeySource.HasValue && Enum.IsDefined(options.KeySource.Value), "Sub2Api.KeySource: required_or_invalid.")
            .Validate(options => options.KeySource != Sub2ApiKeySourceMode.Individual || hasIndividualSource,
                "Sub2Api.KeySource: Individual требует IIndividualModelKeySource приложения до AddAgentBridge.")
            .Validate(options => options.KeySource != Sub2ApiKeySourceMode.Shared || !string.IsNullOrWhiteSpace(options.SharedApiKey),
                "Sub2Api.SharedApiKey: required.")
            .Validate(options => options.SharedApiKey is null || Sub2ApiCredential.IsValid(options.SharedApiKey), "Sub2Api.SharedApiKey: invalid_key.")
            .Validate(options => Sub2ApiEndpoints.TryGetModelsUrl(options.BaseAddress, out _), "Sub2Api.BaseAddress: required_or_invalid.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Sub2Api.Model: required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ReasoningEffort), "Sub2Api.ReasoningEffort: required.")
            .Validate(options => ValidTimeout(options.GenerationTimeout), "Sub2Api.GenerationTimeout: required_or_invalid.")
            .Validate(options => ValidTimeout(options.CompactTimeout), "Sub2Api.CompactTimeout: required_or_invalid.")
            .ValidateOnStart();
    }

    /// <summary>Использует borrowed HTTP приложения без создания клиента при регистрации.</summary>
    internal static void AddPorts(IServiceCollection services, Func<IServiceProvider, HttpClient> httpClientFactory)
    {
        services.AddScoped(provider => new HttpApiClient(httpClientFactory(provider),
            provider.GetRequiredService<ILogger<HttpApiClient>>(), null,
            new HttpClientLoggingOptions { ErrorContentMode = HttpErrorContentLogMode.None },
            new HttpErrorResponseOptions { MaxBodyBytes = 65_536 }));
        services.AddScoped<IModelGateway>(provider => new Sub2ApiModelGateway(
            () => provider.GetRequiredService<HttpApiClient>(), provider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>(),
            provider.GetRequiredService<ILogger<Sub2ApiModelGateway>>(), provider.GetRequiredService<IContextContentInspector>()));
        services.AddScoped<IModelCatalog, Sub2ApiModelCatalog>();
        services.AddScoped<IModelAccessResolver, Sub2ApiModelAccessResolver>();
        services.AddScoped<IModelSettingsReader, Sub2ApiModelSettingsReader>();
    }

    /// <summary>Проверяет диапазон локального deadline .NET.</summary>
    private static bool ValidTimeout(TimeSpan timeout)
    {
        return timeout > TimeSpan.Zero && timeout.TotalMilliseconds <= MAX_TIMEOUT_MILLISECONDS;
    }
}
