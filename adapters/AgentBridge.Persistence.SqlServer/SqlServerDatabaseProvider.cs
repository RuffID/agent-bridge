using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.SqlServer.Options;
using EFCoreLibrary.Maintenance.SqlServer.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.SqlServer;

/// <inheritdoc/>
internal class SqlServerDatabaseProvider : IAgentBridgeDatabaseProvider
{
    /// <inheritdoc/>
    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    /// <inheritdoc/>
    public void Configure(DbContextOptionsBuilder builder, DatabaseOptions options) =>
        builder.UseSqlServer(options.ConnectionString, provider => provider
            .MigrationsAssembly(AgentBridgeMigrationsAssemblies.SQLSERVER)
            .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));

    /// <inheritdoc/>
    public bool IsPrimaryKeyViolation(DbUpdateException error, string table, string constraint) => false;

    /// <inheritdoc/>
    public IDatabaseMaintenanceProvider<AgentBridgeContextKey> CreateMaintenance(IServiceProvider services, DatabaseBackupOptions backup)
    {
        IRelationalMigrationOperations<AgentBridgeContextKey> migrations = services.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        IDatabaseCommands commands = services.GetRequiredService<IDatabaseCommands>();

        return new SqlServerMaintenanceProvider<AgentBridgeContextKey>(migrations, commands,
            new SqlServerMaintenanceOptions(backup.SqlServerBackupDirectory!));
    }
}
