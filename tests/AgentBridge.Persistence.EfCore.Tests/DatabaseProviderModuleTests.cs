using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.Sqlite;
using AgentBridge.Persistence.SqlServer;
using EFCoreLibrary.Maintenance.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет явный выбор модулей и сохранение точной PK classification без БД.</summary>
public class DatabaseProviderModuleTests
{
    /// <summary>Нужен именно выбранный модуль, а не любой установленный provider.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSelectedModuleFailsBeforeDatabase(bool wrongModule)
    {
        ServiceCollection services = new();
        if (wrongModule) services.AddAgentBridgeSqlite();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.SqlServer; options.ConnectionString = "Password=private-secret"; });
        services.AddAgentBridgePersistence();
        using ServiceProvider root = services.BuildServiceProvider();

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => root.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("явно подключённый модуль", error.Message);
        Assert.DoesNotContain("private-secret", error.ToString());
    }

    /// <summary>Повтор подключения того же модуля идемпотентен; другой модуль с тем же выбором отклоняется.</summary>
    [Fact]
    public void RepeatedModuleIsIdempotentAndConflictingModuleIsRejected()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeSqlServer().AddAgentBridgeSqlServer();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.SqlServer; options.ConnectionString = "Server=invalid.example;Database=synthetic"; });
        services.AddAgentBridgePersistence();
        using (ServiceProvider root = services.BuildServiceProvider())
        {
            root.GetRequiredService<IStartupValidator>().Validate();
            Assert.Single(root.GetServices<IAgentBridgeDatabaseProvider>());
        }

        services.AddAgentBridgeDatabaseProvider<ConflictingSqlServerProvider>();
        using ServiceProvider conflicting = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => conflicting.GetRequiredService<IStartupValidator>().Validate());
    }

    /// <summary>Из общей сборки удалены прямые ссылки на драйверы и provider-specific maintenance.</summary>
    [Fact]
    public void CommonAssemblyDoesNotReferenceConcreteProviders()
    {
        string[] references = typeof(AgentBridgeDbContext).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToArray();
        Assert.DoesNotContain(references, name => name.Contains("Sqlite", StringComparison.Ordinal)
            || name.Contains("SqlServer", StringComparison.Ordinal) || name.Contains("Npgsql", StringComparison.Ordinal)
            || name.Contains("PostgreSql", StringComparison.Ordinal));
    }

    /// <summary>SQLite PK и PostgreSQL exact table/constraint сохраняют прежнюю классификацию; unique/FK не становятся PK.</summary>
    [Fact]
    public void PrimaryKeyRecognitionRemainsProviderSpecific()
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        using ServiceProvider root = services.BuildServiceProvider();
        IAgentBridgeDatabaseProvider sqlite = root.GetServices<IAgentBridgeDatabaseProvider>().Single(module => module.Provider == DatabaseProvider.SQLite);
        IAgentBridgeDatabaseProvider postgres = root.GetServices<IAgentBridgeDatabaseProvider>().Single(module => module.Provider == DatabaseProvider.PostgreSql);
        IAgentBridgeDatabaseProvider sqlServer = root.GetServices<IAgentBridgeDatabaseProvider>().Single(module => module.Provider == DatabaseProvider.SqlServer);
        DbUpdateException primary = new("synthetic", new SqliteException("synthetic", 19, 1555));
        Assert.True(sqlite.IsPrimaryKeyViolation(primary, "Dialogs", "PK_Dialogs"));
        Assert.False(sqlite.IsPrimaryKeyViolation(new("synthetic", new SqliteException("synthetic", 19, 2067)), "Dialogs", "PK_Dialogs"));
        Assert.False(sqlServer.IsPrimaryKeyViolation(primary, "Dialogs", "PK_Dialogs"));
        DbUpdateException pgPrimary = new("synthetic", new PostgresException("synthetic", "ERROR", "ERROR", "23505", tableName: "Dialogs", constraintName: "PK_Dialogs"));
        Assert.True(postgres.IsPrimaryKeyViolation(pgPrimary, "Dialogs", "PK_Dialogs"));
        Assert.False(postgres.IsPrimaryKeyViolation(pgPrimary, "DialogSettings", "PK_DialogSettings"));
        Assert.False(postgres.IsPrimaryKeyViolation(pgPrimary, "Dialogs", "OtherUnique"));
    }

    /// <summary>Несогласованный второй модуль; любые операции должны оставаться недоступными.</summary>
    private class ConflictingSqlServerProvider : IAgentBridgeDatabaseProvider
    {
        /// <inheritdoc/>
        public DatabaseProvider Provider => DatabaseProvider.SqlServer;
        /// <inheritdoc/>
        public void Configure(DbContextOptionsBuilder builder, DatabaseOptions options) => throw new InvalidOperationException("No database operation expected.");
        /// <inheritdoc/>
        public bool IsPrimaryKeyViolation(DbUpdateException error, string table, string constraint) => false;
        /// <inheritdoc/>
        public IDatabaseMaintenanceProvider<AgentBridgeContextKey> CreateMaintenance(IServiceProvider services, DatabaseBackupOptions backup) => throw new InvalidOperationException("No maintenance expected.");
    }
}
