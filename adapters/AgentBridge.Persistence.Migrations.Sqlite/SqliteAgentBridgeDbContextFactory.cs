using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentBridge.Persistence.Migrations.Sqlite;

/// <inheritdoc/>
/// <remarks>Создаёт только модель SQLite; не читает конфигурацию приложения и не открывает соединение.</remarks>
public class SqliteAgentBridgeDbContextFactory : IDesignTimeDbContextFactory<AgentBridgeDbContext>
{
    /// <inheritdoc/>
    public AgentBridgeDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 0)
        {
            throw new ArgumentException("Design-time фабрика AgentBridge не принимает аргументы.", nameof(args));
        }
        DbContextOptionsBuilder<AgentBridgeDbContext> builder = new();
        builder.UseSqlite("Data Source=agent-bridge-design-time-never-open.db",
            sqlite => sqlite.MigrationsAssembly(AgentBridgeMigrationsAssemblies.SQLITE)
                .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));
        builder.EnableSensitiveDataLogging(false);
        return new AgentBridgeDbContext(builder.Options);
    }
}
