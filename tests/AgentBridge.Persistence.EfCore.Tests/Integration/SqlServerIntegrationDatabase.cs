using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Opt-in MSSQL fixture с actual AgentBridge/EFCoreLibrary; не выполняет автоматическую очистку ресурсов.</summary>
public class SqlServerIntegrationDatabase : IAsyncDisposable
{
    private readonly SqlServerIntegrationSettings settings;

    /// <summary>Читает явную конфигурацию и создаёт DI, не открывая соединение и не создавая каталоги.</summary>
    public SqlServerIntegrationDatabase()
    {
        settings = SqlServerIntegrationSettings.FromEnvironment();
        Root = BuildRoot();
    }

    /// <summary>Root gate сохраняется между maintenance entrant одного fixture.</summary>
    public ServiceProvider Root { get; }

    /// <summary>Создаёт новый container для restart чтения либо адресной подстановки отказа; не запускает I/O.</summary>
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

    /// <summary>Явно вызывает actual CREATE/migration только после отдельного согласования ресурсов и операций.</summary>
    public async Task<DatabaseMaintenanceResult> InitializeNewAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = Root.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
            .InitializeNewAsync(settings.OperationBudget, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Освобождает только DI. DROP/Down/restore и удаление backup требуют отдельной процедуры с identity check.</remarks>
    public ValueTask DisposeAsync() => Root.DisposeAsync();
}
