using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.CodexLb.Configuration;

/// <summary>Регистрация и локальная проверка настроек codex-lb без подключения транспорта.</summary>
public static class CodexLbConfigurationExtensions
{
    private const double MAX_TIMEOUT_MILLISECONDS = uint.MaxValue - 1d;

    /// <summary>Привязывает переданный раздел codex-lb к типизированным настройкам.</summary>
    public static IServiceCollection AddCodexLbConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        Validate(services.AddOptions<CodexLbOptions>().Bind(configuration));
        return services;
    }

    /// <summary>Настраивает codex-lb программно без файла конфигурации.</summary>
    public static IServiceCollection AddCodexLbConfiguration(this IServiceCollection services, Action<CodexLbOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        Validate(services.AddOptions<CodexLbOptions>().Configure(configure));
        return services;
    }

    /// <summary>Регистрирует локальную валидацию без вывода секретов и без серверного каталога.</summary>
    private static void Validate(OptionsBuilder<CodexLbOptions> builder) => builder
        .Validate(options => !string.IsNullOrWhiteSpace(options.BaseAddress), "CodexLb.BaseAddress обязателен.")
        .Validate(options => string.IsNullOrWhiteSpace(options.BaseAddress) || IsServerAddress(options.BaseAddress),
            "CodexLb.BaseAddress должен быть абсолютным HTTP(S)-адресом без учётных данных, query и fragment.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "CodexLb.Model обязателен; модель задаёт приложение.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.ReasoningEffort), "CodexLb.ReasoningEffort не может быть пустым.")
        .Validate(options => options.SharedApiKey is null || !string.IsNullOrWhiteSpace(options.SharedApiKey),
            "CodexLb.SharedApiKey должен отсутствовать либо содержать непустой ключ.")
        .Validate(options => IsTimeout(options.GenerationTimeout), "CodexLb.GenerationTimeout должен быть положительным и допустимым для таймера .NET.")
        .Validate(options => IsTimeout(options.CompactTimeout), "CodexLb.CompactTimeout должен быть положительным и допустимым для таймера .NET.")
        .ValidateOnStart();

    /// <summary>Проверяет положительный конечный интервал в пределах таймера .NET.</summary>
    private static bool IsTimeout(TimeSpan value) => value > TimeSpan.Zero && value.TotalMilliseconds <= MAX_TIMEOUT_MILLISECONDS;

    /// <summary>Проверяет адрес сервера, исключая встроенные секреты и неподдержанные схемы.</summary>
    private static bool IsServerAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
