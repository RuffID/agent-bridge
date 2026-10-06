using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.Configuration;
using AgentBridge.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Пример composition root без запуска приложения; фабрики и секреты предоставляет приложение.</summary>
public static class UsageRegistration
{
    /// <summary>Регистрирует библиотеку и явно выбранные зависимости приложения без операций БД/HTTP.</summary>
    /// <remarks>Приложение владеет HttpClient/handlers без retry и смены ключа. Фабрики не выполняют I/O.
    /// Провайдеры возвращаются в нужном порядке, scoped бизнес-сервис не разделяется между tool tasks.
    /// ILoggerFactory заранее зарегистрирован приложением; callback получает app-owned client с управляемым сроком.
    /// Logging provider, авторизация, инициализация БД и расписание очистки остаются у приложения.</remarks>
    public static IServiceCollection AddUsageGuide(this IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, HttpClient> httpClientFactory,
        Func<IServiceProvider, IIndividualModelKeySource> individualKeyFactory,
        Func<IServiceProvider, IReadOnlyList<IContextProvider>> orderedProvidersFactory,
        Func<IServiceProvider, IAccountSummarySource> accountSummaryFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(individualKeyFactory);
        ArgumentNullException.ThrowIfNull(orderedProvidersFactory);
        ArgumentNullException.ThrowIfNull(accountSummaryFactory);
        services.AddScoped<IIndividualModelKeySource>(individualKeyFactory);
        services.AddScoped<ContextBuilder>(provider => new ContextBuilder(orderedProvidersFactory(provider)));
        services.AddScoped<IAccountSummarySource>(accountSummaryFactory);
        services.AddAgentBridgeTool<AccountSummaryTool, AccountSummaryValidator>(AccountSummaryTool.ToolDefinition);
        return services.AddAgentBridge(configuration, httpClientFactory);
    }
}
