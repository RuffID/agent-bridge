using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Diagnostics;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentBridge.Integration;

/// <summary>Стандартная композиция ядра и адаптеров без запуска приложения или внешних операций.</summary>
public static class AgentBridgeIntegrationExtensions
{
    /// <summary>Подключает options, хранение, transport и прикладные сценарии, сохраняя зависимости приложения.</summary>
    /// <param name="services">Контейнер с заранее зарегистрированным ILoggerFactory и optional индивидуальным источником ключей.</param>
    /// <param name="configuration">Конфигурация приложения с разделами AgentBridge, CodexLb и Database.</param>
    /// <param name="httpClientFactory">Scoped callback получения принадлежащего приложению HttpClient без I/O, retry и смены аккаунта.</param>
    /// <remarks>Приложение владеет client/handlers/timeout/disposal, logger/sinks, авторизацией, business tools,
    /// обслуживанием БД и расписанием. Повтор с теми же объектами config/factory идемпотентен;
    /// другие аргументы отклоняются. Individual source регистрируется до этого вызова.
    /// Options проверяются при получении или явном IStartupValidator.Validate до операций.</remarks>
    public static IServiceCollection AddAgentBridge(this IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        Registration? registration = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(Registration))
            ?.ImplementationInstance as Registration;
        if (registration is not null)
        {
            if (!ReferenceEquals(registration.Configuration, configuration) || !ReferenceEquals(registration.HttpClientFactory, httpClientFactory))
                throw new InvalidOperationException("AddAgentBridge уже подключён с другой configuration или HTTP factory.");
            return services;
        }
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(ILoggerFactory)))
            throw new InvalidOperationException("AddAgentBridge требует ILoggerFactory приложения; сначала настройте logging.");

        bool hasIndividualSource = services.Any(descriptor => descriptor.ServiceType == typeof(IIndividualModelKeySource));
        services.AddAgentBridgeConfiguration(configuration.GetSection("AgentBridge"));
        services.AddCodexLbConfiguration(configuration.GetSection("CodexLb"));
        services.AddDatabaseConfiguration(configuration.GetSection("Database"));
        services.AddOptions<CodexLbOptions>().Validate(options => options.KeySource != ModelKeySourceMode.Individual || hasIndividualSource,
            "CodexLb.KeySource: Individual требует IIndividualModelKeySource приложения до AddAgentBridge.");
        services.TryAddScoped<IIndividualModelKeySource, SharedKeySource>();

        ServiceCollection modules = new();
        modules.AddAgentBridgePersistence();
        modules.AddCodexLbResponses(httpClientFactory);
        foreach (ServiceDescriptor descriptor in modules)
        {
            // Options delegates дополняют pipeline; остальные регистрации являются заменяемыми defaults.
            if (descriptor.ServiceType.IsGenericType && descriptor.ServiceType.GetGenericTypeDefinition()
                == typeof(IDbContextOptionsConfiguration<>))
                services.Add(descriptor);
            else if (descriptor.ServiceType.IsGenericType && descriptor.ServiceType.GetGenericTypeDefinition()
                == typeof(IValidateOptions<>))
                services.TryAddEnumerable(descriptor);
            else
                services.TryAdd(descriptor);
        }
        services.AddAgentBridgeDiagnostics();
        services.TryAddScoped<ContextBuilder>(provider => new ContextBuilder(provider.GetServices<IContextProvider>()));
        services.AddAgentBridgeTools();
        services.AddAgentBridgeTokenization();
        services.AddAgentBridgeCompaction();
        services.AddAgentBridgeRunner();
        services.AddAgentBridgeSettings();
        services.AddAgentBridgeDialogCleanup();
        services.AddSingleton(new Registration(configuration, httpClientFactory));
        return services;
    }

    /// <summary>Фиксирует аргументы единственного подключения без ServiceProvider или операций.</summary>
    private record Registration(IConfiguration Configuration, Func<IServiceProvider, HttpClient> HttpClientFactory);

    /// <summary>Отсутствие индивидуального ключа в явно выбранном Shared режиме.</summary>
    private class SharedKeySource : IIndividualModelKeySource
    {
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(ownerId);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }
    }
}
