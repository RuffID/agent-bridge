using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgentBridge.Configuration;

/// <summary>Явная регистрация offline счётчика и проверки бюджета в composition root приложения.</summary>
public static class AgentBridgeTokenizationExtensions
{
    /// <summary>Сохраняет явные регистрации приложения; не запускает tokenizer/HTTP/DB или compact.</summary>
    public static IServiceCollection AddAgentBridgeTokenization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddValidatedOptions(services);
        services.TryAddSingleton<IContextTokenCounter, ContextTokenCounter>();
        services.TryAddTransient<ContextBudgetGuard>();
        return services;
    }

    /// <summary>Добавляет exact соответствия приложения с fail-fast проверкой, сохраняя пользовательский counter.</summary>
    /// <param name="services">Контейнер приложения.</param>
    /// <param name="configure">Настройка дополнительного списка без замены подтверждённых встроенных кодировок.</param>
    public static IServiceCollection AddAgentBridgeTokenization(this IServiceCollection services,
        Action<TokenizationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddAgentBridgeTokenization();
        services.Configure(configure);
        return services;
    }

    /// <summary>Подключает стандартный options pipeline и однократный validator без создания counter.</summary>
    internal static OptionsBuilder<TokenizationOptions> AddValidatedOptions(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TokenizationOptions>, TokenizationOptionsValidator>());
        return services.AddOptions<TokenizationOptions>().ValidateOnStart();
    }
}
