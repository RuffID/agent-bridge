using AgentBridge.Application.Ports;
using AgentBridge.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.Sub2Api.Configuration;

/// <summary>Подготовка composition этапа02; не реализует каталог, доступ и HTTP этапа03.</summary>
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
            .Validate(options => options.SharedApiKey is null || ValidKey(options.SharedApiKey), "Sub2Api.SharedApiKey: invalid_key.")
            .Validate(options => ValidAddress(options.BaseAddress), "Sub2Api.BaseAddress: required_or_invalid.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Sub2Api.Model: required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ReasoningEffort), "Sub2Api.ReasoningEffort: required.")
            .Validate(options => ValidTimeout(options.GenerationTimeout), "Sub2Api.GenerationTimeout: required_or_invalid.")
            .Validate(options => ValidTimeout(options.CompactTimeout), "Sub2Api.CompactTimeout: required_or_invalid.")
            .ValidateOnStart();
    }

    /// <summary>Все четыре порта относятся к одному staged connector и возвращают явный Unsupported.</summary>
    internal static void AddPorts(IServiceCollection services)
    {
        services.AddScoped<IModelGateway, Sub2ApiPendingConnector>();
        services.AddScoped<IModelCatalog, Sub2ApiPendingConnector>();
        services.AddScoped<IModelAccessResolver, Sub2ApiPendingConnector>();
        services.AddScoped<IModelSettingsReader, Sub2ApiPendingConnector>();
    }

    /// <summary>Проверяет только пригодность локального значения Bearer.</summary>
    private static bool ValidKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && !key.Any(char.IsWhiteSpace) && !key.Any(char.IsControl);
    }

    /// <summary>Проверяет адрес без выполнения нормализации маршрутов этапа03.</summary>
    private static bool ValidAddress(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    }

    /// <summary>Проверяет диапазон локального deadline .NET.</summary>
    private static bool ValidTimeout(TimeSpan timeout)
    {
        return timeout > TimeSpan.Zero && timeout.TotalMilliseconds <= MAX_TIMEOUT_MILLISECONDS;
    }
}
