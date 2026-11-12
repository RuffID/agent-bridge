using System.Data;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.Migrations.SqlServer;
using AgentBridge.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет восстановленную схему и public DI без подключения к серверу.</summary>
public class SqlServerModelCompatibilityTests
{
    /// <summary>Runtime, snapshot и последний designer описывают одну схему с прежними migration IDs.</summary>
    [Fact]
    public void RuntimeMatchesRestoredMigrations()
    {
        // Arrange
        using AgentBridgeDbContext design = new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]);
        ServiceCollection services = new();
        services.AddAgentBridgeSqlServer().AddAgentBridgeSqlServer();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = design.Database.GetConnectionString()!;
        });
        services.AddAgentBridgePersistence();
        using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        root.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope scope = root.CreateScope();

        // Act
        AgentBridgeDbContext runtime = scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        IMigrationsAssembly assembly = runtime.GetService<IMigrationsAssembly>();
        Migration latest = assembly.CreateMigration(assembly.Migrations.OrderBy(pair => pair.Key).Last().Value,
            runtime.Database.ProviderName!);
        IModelRuntimeInitializer initializer = runtime.GetService<IModelRuntimeInitializer>();
        IModel snapshot = initializer.Initialize(assembly.ModelSnapshot!.Model, designTime: true);
        IModel target = initializer.Initialize(latest.TargetModel, designTime: true);
        IModel current = runtime.GetService<IDesignTimeModel>().Model;
        IMigrationsModelDiffer differ = runtime.GetService<IMigrationsModelDiffer>();

        // Assert
        Assert.Equal(new[] { "20261006060948_InitialAgentBridgeSchema", "20261011024148_AddCatalogAndDurableRecovery" },
            assembly.Migrations.Keys.Order(StringComparer.Ordinal));
        Assert.Single(root.GetServices<IAgentBridgeDatabaseProvider>());
        Assert.Equal(typeof(SqlServerAgentBridgeDbContextFactory).Assembly, assembly.Assembly);
        Assert.Equal("__AgentBridgeMigrationsHistory",
            RelationalOptionsExtension.Extract(runtime.GetService<IDbContextOptions>()).MigrationsHistoryTableName);
        Assert.False(differ.HasDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel()));
        Assert.False(differ.HasDifferences(target.GetRelationalModel(), snapshot.GetRelationalModel()));
        Assert.Equal(design.GetService<IDesignTimeModel>().Model.ToDebugString(MetadataDebugStringOptions.LongDefault),
            current.ToDebugString(MetadataDebugStringOptions.LongDefault));
        Assert.Equal(ConnectionState.Closed, runtime.Database.GetDbConnection().State);
    }

    /// <summary>Конфигурационный путь maintenance не требует PostgreSQL полей и не открывает соединение.</summary>
    [Fact]
    public void ServerBackupConfigurationDoesNotRequirePostgreSqlOptions()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlServerBackupDirectory"] = "/var/opt/mssql/backups",
                ["BackupRetentionPeriod"] = "7.00:00:00"
            }).Build();
        ServiceCollection services = new();
        services.AddAgentBridgeSqlServer();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = "Server=invalid.example;Database=synthetic;Encrypt=True";
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(configuration);
        using ServiceProvider root = services.BuildServiceProvider();

        // Act
        root.GetRequiredService<IStartupValidator>().Validate();
        DatabaseBackupOptions options = root.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value;

        // Assert
        Assert.Equal("/var/opt/mssql/backups", options.SqlServerBackupDirectory);
        Assert.Null(options.BackupDirectory);
        Assert.Null(options.PostgreSqlDumpExecutablePath);
    }
}
