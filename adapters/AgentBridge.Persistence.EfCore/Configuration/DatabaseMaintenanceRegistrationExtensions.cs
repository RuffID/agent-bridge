using System.Globalization;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Extensions;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.Options;
using EFCoreLibrary.Maintenance.PostgreSql.Providers;
using EFCoreLibrary.Maintenance.Sqlite.Abstractions;
using EFCoreLibrary.Maintenance.Sqlite.Backup;
using EFCoreLibrary.Maintenance.Sqlite.Options;
using EFCoreLibrary.Maintenance.Sqlite.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Подключение библиотечного обслуживания к контексту AgentBridge без выполнения операций.</summary>
public static class DatabaseMaintenanceRegistrationExtensions
{
    /// <summary>Привязывает раздел backup; приложение явно выбирает SingleInitializer и затем вызывает maintenance в отдельном scope.</summary>
    public static IServiceCollection AddAgentBridgeDatabaseMaintenance(
        this IServiceCollection services, IConfiguration configuration, MaintenanceExecutionMode mode)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateMode(mode);
        services.AddOptions<DatabaseBackupOptions>().Bind(configuration).ValidateOnStart();
        return Register(services, mode);
    }

    /// <summary>Настраивает backup программно без host; срок хранения обязателен и не имеет default.</summary>
    public static IServiceCollection AddAgentBridgeDatabaseMaintenance(
        this IServiceCollection services, Action<DatabaseBackupOptions> configure, MaintenanceExecutionMode mode)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ValidateMode(mode);
        services.AddOptions<DatabaseBackupOptions>().Configure(configure).ValidateOnStart();
        return Register(services, mode);
    }

    /// <summary>Оставляет orchestration, EF migrations, gate и process ownership в EFCoreLibrary.</summary>
    private static IServiceCollection Register(IServiceCollection services, MaintenanceExecutionMode mode)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseBackupOptions>, DatabaseBackupOptionsValidator>());
        services.AddRelationalMaintenance<AgentBridgeContextKey>(mode);
        services.TryAddSingleton<ISqliteBackupApi, SqliteBackupApi>();
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(provider =>
        {
            DatabaseBackupOptions backup = provider.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value;
            DatabaseOptions database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            IRelationalMigrationOperations<AgentBridgeContextKey> migrations = provider.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>();
            IDatabaseCommands commands = provider.GetRequiredService<IDatabaseCommands>();
            return database.Provider switch
            {
                DatabaseProvider.SQLite => new SqliteMaintenanceProvider<AgentBridgeContextKey>(migrations, commands,
                    new SqliteMaintenanceOptions(backup.BackupDirectory!), provider.GetRequiredService<ISqliteBackupApi>(),
                    provider.GetRequiredService<IMaintenanceRecovery>()),
                DatabaseProvider.PostgreSql => new PostgreSqlMaintenanceProvider<AgentBridgeContextKey>(migrations, commands,
                    new DumpOptions(backup.PostgreSqlDumpExecutablePath!, backup.BackupDirectory!,
                        backup.PostgreSqlServerMajorVersion!.Value.ToString(CultureInfo.InvariantCulture), backup.PostgreSqlCleanupTimeout!.Value),
                    provider.GetRequiredService<IBackupProcessRunner>()),
                _ => throw new MaintenanceException(MaintenanceError.Configuration)
            };
        });
        return services;
    }

    /// <summary>Отклоняет неявный или неизвестный режим до изменения регистраций.</summary>
    private static void ValidateMode(MaintenanceExecutionMode mode)
    {
        if (mode != MaintenanceExecutionMode.SingleInitializer)
        {
            throw new MaintenanceException(MaintenanceError.Configuration);
        }
    }
}
