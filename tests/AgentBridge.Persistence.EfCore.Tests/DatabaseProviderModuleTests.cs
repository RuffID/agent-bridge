using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.PostgreSql;
using EFCoreLibrary.Maintenance.Abstractions;
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
    [Fact]
    public void MissingSelectedModuleFailsBeforeDatabase()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.PostgreSql; options.ConnectionString = "Password=private-secret"; });
        services.AddAgentBridgePersistence();
        using ServiceProvider root = services.BuildServiceProvider();

        // Act
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => root.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        Assert.Contains("явно подключённый модуль", error.Message);
        Assert.DoesNotContain("private-secret", error.ToString());
    }

    /// <summary>Повтор подключения того же модуля идемпотентен; другой модуль с тем же выбором отклоняется.</summary>
    [Fact]
    public void RepeatedModuleIsIdempotentAndConflictingModuleIsRejected()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddAgentBridgePostgreSql().AddAgentBridgePostgreSql();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.PostgreSql; options.ConnectionString = "Host=invalid.example;Database=synthetic"; });
        services.AddAgentBridgePersistence();

        // Act
        using (ServiceProvider root = services.BuildServiceProvider())
        {
            root.GetRequiredService<IStartupValidator>().Validate();

            // Assert
            Assert.Single(root.GetServices<IAgentBridgeDatabaseProvider>());
        }

        // Arrange
        services.AddAgentBridgeDatabaseProvider<ConflictingPostgreSqlProvider>();
        using ServiceProvider conflicting = services.BuildServiceProvider();

        // Act
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => conflicting.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        Assert.Contains("явно подключённый модуль", error.Message);
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

    /// <summary>PostgreSQL exact table/constraint сохраняет прежнюю классификацию; unique/FK не становятся PK.</summary>
    [Fact]
    public void PrimaryKeyRecognitionRemainsProviderSpecific()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        using ServiceProvider root = services.BuildServiceProvider();
        IAgentBridgeDatabaseProvider postgres = root.GetServices<IAgentBridgeDatabaseProvider>().Single(module => module.Provider == DatabaseProvider.PostgreSql);

        // Act
        DbUpdateException pgPrimary = new("synthetic", new PostgresException("synthetic", "ERROR", "ERROR", "23505", tableName: "Dialogs", constraintName: "PK_Dialogs"));
        bool recognized = postgres.IsPrimaryKeyViolation(pgPrimary, "Dialogs", "PK_Dialogs");

        // Assert
        Assert.True(recognized);
        Assert.False(postgres.IsPrimaryKeyViolation(pgPrimary, "DialogSettings", "PK_DialogSettings"));
        Assert.False(postgres.IsPrimaryKeyViolation(pgPrimary, "Dialogs", "OtherUnique"));
    }

    /// <summary>Несогласованный второй модуль; любые операции должны оставаться недоступными.</summary>
    private class ConflictingPostgreSqlProvider : IAgentBridgeDatabaseProvider
    {
        /// <inheritdoc/>
        public DatabaseProvider Provider => DatabaseProvider.PostgreSql;
        /// <inheritdoc/>
        public void Configure(DbContextOptionsBuilder builder, DatabaseOptions options)
        {
            throw new InvalidOperationException("No database operation expected.");
        }
        /// <inheritdoc/>
        public bool IsPrimaryKeyViolation(DbUpdateException error, string table, string constraint)
        {
            return false;
        }
        /// <inheritdoc/>
        public IDatabaseMaintenanceProvider<AgentBridgeContextKey> CreateMaintenance(IServiceProvider services, DatabaseBackupOptions backup)
        {
            throw new InvalidOperationException("No maintenance expected.");
        }
    }
}
