using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Diagnostics;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Пример composition root без запуска приложения; фабрики и секреты предоставляет приложение.</summary>
public static class UsageRegistration
{
    /// <summary>Регистрирует библиотеку и явно выбранные зависимости приложения без операций БД/HTTP.</summary>
    /// <remarks>Приложение владеет HttpClient/handlers без retry и смены ключа. Фабрики не выполняют I/O.
    /// Провайдеры возвращаются в нужном порядке, scoped бизнес-сервис не разделяется между tool tasks.
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
        services.AddAgentBridgeConfiguration(configuration.GetSection("AgentBridge"));
        services.AddCodexLbConfiguration(configuration.GetSection("CodexLb"));
        services.AddDatabaseConfiguration(configuration.GetSection("Database"));
        services.AddAgentBridgeDiagnostics();
        services.AddAgentBridgePersistence();
        services.AddScoped<IIndividualModelKeySource>(individualKeyFactory);
        services.AddCodexLbResponses(httpClientFactory);
        services.AddScoped<ContextBuilder>(provider => new ContextBuilder(orderedProvidersFactory(provider)));
        services.AddScoped<IAccountSummarySource>(accountSummaryFactory);
        services.AddAgentBridgeTool<AccountSummaryTool, AccountSummaryValidator>(AccountSummaryTool.ToolDefinition);
        services.AddAgentBridgeTokenization();
        services.AddAgentBridgeCompaction();
        services.AddAgentBridgeRunner();
        services.AddAgentBridgeSettings();
        return services.AddAgentBridgeDialogCleanup();
    }
}
