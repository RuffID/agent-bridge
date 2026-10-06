using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentBridge.Persistence.Migrations.SqlServer;

/// <inheritdoc/>
/// <remarks>Создаёт options и модель SQL Server без host, чтения secrets или открытия соединения.</remarks>
public class SqlServerAgentBridgeDbContextFactory : IDesignTimeDbContextFactory<AgentBridgeDbContext>
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
        builder.UseSqlServer("Server=invalid.example;Database=agent_bridge_design_time;User ID=synthetic;Password=synthetic;Encrypt=True;TrustServerCertificate=False;ConnectRetryCount=0",
            sqlServer => sqlServer.MigrationsAssembly(AgentBridgeMigrationsAssemblies.SQLSERVER)
                .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));
        builder.EnableSensitiveDataLogging(false);
        return new AgentBridgeDbContext(builder.Options);
    }
}
