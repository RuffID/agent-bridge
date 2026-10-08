using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Владеет DI для собственной БД в контейнере коллекции; операции выполняет AgentBridge/EFCoreLibrary.</summary>
public class SqlServerIntegrationDatabase : IAsyncDisposable
{
    private readonly SqlServerIntegrationSettings settings;

    /// <summary>Принимает проверенные ресурсы fixture и создаёт DI без открытия соединения.</summary>
    internal SqlServerIntegrationDatabase(SqlServerIntegrationSettings settings)
    {
        this.settings = settings;
        Root = BuildRoot();
    }

    /// <summary>Root gate сохраняется между maintenance entrant одного fixture.</summary>
    public ServiceProvider Root { get; }

    /// <summary>Создаёт новый DI root для чтения либо подстановки отказа; SQL Server не перезапускается.</summary>
    public ServiceProvider BuildRoot(Action<ServiceCollection>? customize = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = settings.ConnectionString;
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            options.SqlServerBackupDirectory = settings.ServerBackupDirectory;
            options.BackupRetentionPeriod = TimeSpan.FromDays(1);
        }, MaintenanceExecutionMode.SingleInitializer);
        customize?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Явно создаёт собственную БД и применяет миграции настоящим maintenance API.</summary>
    public async Task<DatabaseMaintenanceResult> InitializeNewAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = Root.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
            .InitializeNewAsync(settings.OperationBudget, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Освобождает DI; БД и серверные файлы удаляются вместе с собственным контейнером коллекции.</remarks>
    public ValueTask DisposeAsync() => Root.DisposeAsync();
}
