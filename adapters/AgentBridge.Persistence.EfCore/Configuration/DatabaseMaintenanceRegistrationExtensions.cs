using AgentBridge.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Подключение библиотечного обслуживания к контексту AgentBridge без выполнения операций.</summary>
public static class DatabaseMaintenanceRegistrationExtensions
{
    /// <summary>Привязывает раздел backup; приложение обеспечивает единственного исполнителя и вызывает maintenance в отдельном scope.</summary>
    public static IServiceCollection AddAgentBridgeDatabaseMaintenance(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseBackupOptions>().BindSafely(configuration, "Backup")
            .Configure(_ => SafeOptionsBindingExtensions.RequireValues<DatabaseBackupOptions>(
                configuration, "Backup", Options.DefaultName,
                nameof(DatabaseBackupOptions.BackupDirectory),
                nameof(DatabaseBackupOptions.BackupRetentionPeriod),
                nameof(DatabaseBackupOptions.PostgreSqlDumpExecutablePath),
                nameof(DatabaseBackupOptions.PostgreSqlServerMajorVersion),
                nameof(DatabaseBackupOptions.PostgreSqlCleanupTimeout)))
            .ValidateOnStart();

        return Register(services);
    }

    /// <summary>Настраивает backup программно без host; срок хранения обязателен и не имеет default.</summary>
    public static IServiceCollection AddAgentBridgeDatabaseMaintenance(
        this IServiceCollection services, Action<DatabaseBackupOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<DatabaseBackupOptions>().Configure(configure).ValidateOnStart();

        return Register(services);
    }

    /// <summary>Оставляет orchestration, EF migrations, gate и process ownership в EFCoreLibrary.</summary>
    private static IServiceCollection Register(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseBackupOptions>, DatabaseBackupOptionsValidator>());
        services.AddRelationalMaintenance<AgentBridgeContextKey>();
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(provider =>
        {
            DatabaseBackupOptions backup = provider.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value;
            DatabaseOptions database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return DatabaseProviderResolver.Resolve(provider, database).CreateMaintenance(provider, backup);
        });

        return services;
    }
}
