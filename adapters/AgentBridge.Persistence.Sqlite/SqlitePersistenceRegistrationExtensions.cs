using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Sqlite.Abstractions;
using EFCoreLibrary.Maintenance.Sqlite.Backup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Persistence.Sqlite;

/// <summary>Явное подключение Sqlite без регистрации других провайдеров, подключения к БД или запуска обслуживания.</summary>
public static class SqlitePersistenceRegistrationExtensions
{
    /// <summary>Регистрирует модуль Sqlite; Database.Provider должен выбрать его перед созданием контекста.</summary>
    public static IServiceCollection AddAgentBridgeSqlite(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISqliteBackupApi, SqliteBackupApi>();
        services.AddAgentBridgeDatabaseProvider<SqliteDatabaseProvider>();

        return services;
    }
}
