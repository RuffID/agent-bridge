using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using System.Globalization;
using EFCoreLibrary.Maintenance.Options;
using EFCoreLibrary.Maintenance.PostgreSql.Providers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.PostgreSql;

/// <inheritdoc/>
internal class PostgreSqlDatabaseProvider : IAgentBridgeDatabaseProvider
{
    /// <inheritdoc/>
    public DatabaseProvider Provider => DatabaseProvider.PostgreSql;

    /// <inheritdoc/>
    public void Configure(DbContextOptionsBuilder builder, DatabaseOptions options) =>
        builder.UseNpgsql(options.ConnectionString, provider => provider
            .MigrationsAssembly(AgentBridgeMigrationsAssemblies.POSTGRESQL)
            .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));

    /// <inheritdoc/>
    public bool IsPrimaryKeyViolation(DbUpdateException error, string table, string constraint) =>
        error.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.TableName == table && postgres.ConstraintName == constraint;

    /// <inheritdoc/>
    public IDatabaseMaintenanceProvider<AgentBridgeContextKey> CreateMaintenance(IServiceProvider services, DatabaseBackupOptions backup)
    {
        IRelationalMigrationOperations<AgentBridgeContextKey> migrations = services.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        IDatabaseCommands commands = services.GetRequiredService<IDatabaseCommands>();

        return new PostgreSqlMaintenanceProvider<AgentBridgeContextKey>(migrations, commands,
            new DumpOptions(backup.PostgreSqlDumpExecutablePath!, backup.BackupDirectory!,
                backup.PostgreSqlServerMajorVersion!.Value.ToString(CultureInfo.InvariantCulture), backup.PostgreSqlCleanupTimeout!.Value),
            services.GetRequiredService<IBackupProcessRunner>());
    }
}
