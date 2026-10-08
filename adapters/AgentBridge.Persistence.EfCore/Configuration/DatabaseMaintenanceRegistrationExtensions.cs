using AgentBridge.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Extensions;
using EFCoreLibrary.Maintenance.Models;
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
        services.AddOptions<DatabaseBackupOptions>().BindSafely(configuration, "Backup")
            .Configure<IOptions<DatabaseOptions>>((options, database) =>
            {
                List<string> required = [nameof(DatabaseBackupOptions.BackupRetentionPeriod)];
                required.Add(database.Value.Provider == DatabaseProvider.SqlServer
                    ? nameof(DatabaseBackupOptions.SqlServerBackupDirectory) : nameof(DatabaseBackupOptions.BackupDirectory));
                if (database.Value.Provider == DatabaseProvider.PostgreSql)
                {
                    required.AddRange([nameof(DatabaseBackupOptions.PostgreSqlDumpExecutablePath),
                        nameof(DatabaseBackupOptions.PostgreSqlServerMajorVersion), nameof(DatabaseBackupOptions.PostgreSqlCleanupTimeout)]);
                }
                SafeOptionsBindingExtensions.RequireValues<DatabaseBackupOptions>(configuration, "Backup", Options.DefaultName, required.ToArray());
            }).ValidateOnStart();
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
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(provider =>
        {
            DatabaseBackupOptions backup = provider.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value;
            DatabaseOptions database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return DatabaseProviderResolver.Resolve(provider, database).CreateMaintenance(provider, backup);
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
