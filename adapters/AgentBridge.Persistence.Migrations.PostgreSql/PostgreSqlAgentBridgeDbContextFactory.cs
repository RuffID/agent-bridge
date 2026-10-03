using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentBridge.Persistence.Migrations.PostgreSql;

/// <inheritdoc/>
/// <remarks>Создаёт только модель PostgreSQL; не читает конфигурацию приложения и не открывает соединение.</remarks>
public class PostgreSqlAgentBridgeDbContextFactory : IDesignTimeDbContextFactory<AgentBridgeDbContext>
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
        builder.UseNpgsql("Host=invalid.example;Database=agent_bridge_design_time;Username=synthetic;Password=synthetic",
            postgres => postgres.MigrationsAssembly(AgentBridgeMigrationsAssemblies.POSTGRESQL)
                .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));
        builder.EnableSensitiveDataLogging(false);
        return new AgentBridgeDbContext(builder.Options);
    }
}
