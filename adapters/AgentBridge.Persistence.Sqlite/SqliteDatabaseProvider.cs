using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Sqlite.Abstractions;
using EFCoreLibrary.Maintenance.Sqlite.Backup;
using EFCoreLibrary.Maintenance.Sqlite.Options;
using EFCoreLibrary.Maintenance.Sqlite.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.Sqlite;

/// <inheritdoc/>
internal class SqliteDatabaseProvider : IAgentBridgeDatabaseProvider
{
    /// <inheritdoc/>
    public DatabaseProvider Provider => DatabaseProvider.SQLite;

    /// <inheritdoc/>
    public void Configure(DbContextOptionsBuilder builder, DatabaseOptions options) =>
        builder.UseSqlite(options.ConnectionString, provider => provider
            .MigrationsAssembly(AgentBridgeMigrationsAssemblies.SQLITE)
            .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));

    /// <inheritdoc/>
    public bool IsPrimaryKeyViolation(DbUpdateException error, string table, string constraint) =>
        error.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 };

    /// <inheritdoc/>
    public IDatabaseMaintenanceProvider<AgentBridgeContextKey> CreateMaintenance(IServiceProvider services, DatabaseBackupOptions backup)
    {
        IRelationalMigrationOperations<AgentBridgeContextKey> migrations = services.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        IDatabaseCommands commands = services.GetRequiredService<IDatabaseCommands>();

        return new SqliteMaintenanceProvider<AgentBridgeContextKey>(migrations, commands,
            new SqliteMaintenanceOptions(backup.BackupDirectory!), services.GetRequiredService<ISqliteBackupApi>(),
            services.GetRequiredService<IMaintenanceRecovery>());
    }
}
