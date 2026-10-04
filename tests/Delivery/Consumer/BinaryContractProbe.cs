using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Models;
using HttpClientLibrary.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Проверяет доступность бинарных контрактов компилятору; методы в этапе24 не исполняются.</summary>
public static class BinaryContractProbe
{
    /// <summary>Компилирует ссылки на регистрацию и настройки; не является полным composition root приложения.</summary>
    public static IServiceCollection Register(IServiceCollection services, IConfiguration configuration,
        DatabaseProvider provider, string connectionString, Func<IServiceProvider, HttpClient> httpClientFactory,
        Action<DatabaseBackupOptions> backup)
    {
        services.AddAgentBridgeConfiguration(configuration.GetSection("AgentBridge"));
        services.AddCodexLbConfiguration(configuration.GetSection("CodexLb"));
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = provider;
            options.ConnectionString = connectionString;
        });
        services.AddAgentBridgePersistence();
        services.AddCodexLbResponses(httpClientFactory);
        services.AddAgentBridgeTokenization();
        services.AddAgentBridgeCompaction();
        services.AddAgentBridgeTools();
        services.AddAgentBridgeRunner();
        services.AddAgentBridgeSettings();
        services.AddAgentBridgeDialogCleanup();
        return services.AddAgentBridgeDatabaseMaintenance(backup, MaintenanceExecutionMode.SingleInitializer);
    }

    /// <summary>Проверяет прямую доступность типов ядра, адаптера, EFCoreLibrary и HttpClientLibrary.</summary>
    public static Type[] Contracts() =>
    [
        typeof(AgentRunner), typeof(AgentSettingsService), typeof(ExpiredDialogCleanup),
        typeof(IModelGateway), typeof(IContextTokenCounter), typeof(IDialogReader),
        typeof(AgentBridgeDbContext), typeof(IDatabaseMaintenance<AgentBridgeContextKey>),
        typeof(IUnitOfWorkContext<AgentBridgeContextKey>), typeof(IHttpApiClient)
    ];
}
