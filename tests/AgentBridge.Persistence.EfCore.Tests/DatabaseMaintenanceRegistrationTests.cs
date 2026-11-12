using System.Data;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.PostgreSql.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Настоящие options, registration и provider metadata без maintenance I/O или host.</summary>
public class DatabaseMaintenanceRegistrationTests
{
    /// <summary>DI выбирает библиотечный provider и общий gate, сохраняет scoped context и закрытый connection.</summary>
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, "postgresql", "custom", "selected database; excludes global roles", true)]
    public void RegistrationAndResolutionDoNotStartMaintenance(DatabaseProvider selected, string name, string format, string scopeName, bool external)
    {
        // Arrange
        ServiceCollection services = Services(selected);
        using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        root.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();

        // Act
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = first.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();

        // Assert
        Assert.IsType<DatabaseMaintenance<AgentBridgeContextKey>>(maintenance);
        Assert.Equal(new DatabaseMaintenanceCapabilities(name, format, scopeName, external, false), maintenance.Capabilities);
        object provider = first.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
        Assert.Equal(typeof(PostgreSqlMaintenanceProvider<AgentBridgeContextKey>), provider.GetType());
        Assert.Same(maintenance, first.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>());
        Assert.NotSame(maintenance, second.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>());
        Assert.Same(first.ServiceProvider.GetRequiredService<SingleInitializerGate>(), second.ServiceProvider.GetRequiredService<SingleInitializerGate>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(SingleInitializerGate));
        Assert.Equal(ServiceLifetime.Singleton, services.Single(descriptor => descriptor.ServiceType == typeof(SingleInitializerGate)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, services.Single(descriptor => descriptor.ServiceType == typeof(IDatabaseMaintenance<AgentBridgeContextKey>)).Lifetime);
        AgentBridgeDbContext context = first.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        IRelationalMigrationOperations<AgentBridgeContextKey> migrations = first.ServiceProvider.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        Assert.Same(context.Database.GetDbConnection(), migrations.Connection);
        Assert.Equal(ConnectionState.Closed, migrations.Connection.State);
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
        Assert.Throws<InvalidOperationException>(() => root.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>());
    }

    /// <summary>Старая регистрация persistence сама по себе не подключает maintenance.</summary>
    [Fact]
    public void PersistenceRegistrationAloneDoesNotAddMaintenance()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.PostgreSql; options.ConnectionString = "Host=invalid.example;Database=synthetic"; });

        // Act
        services.AddAgentBridgePersistence();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDatabaseMaintenance<AgentBridgeContextKey>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(SingleInitializerGate));
    }

    /// <summary>Отсутствующая настройка backup отклоняется до изменения DI.</summary>
    [Fact]
    public void BackupConfigurationMustBeProvided()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() =>
            services.AddAgentBridgeDatabaseMaintenance((Action<DatabaseBackupOptions>)null!));

        // Assert
        Assert.Equal("configure", error.ParamName);
        Assert.Empty(services);
    }

    /// <summary>Локальные ошибки backup не раскрывают значения и останавливают разрешение до контекста.</summary>
    [Theory]
    [InlineData("directory-missing")]
    [InlineData("directory-relative")]
    [InlineData("directory-control")]
    [InlineData("retention-missing")]
    [InlineData("retention-zero")]
    [InlineData("retention-negative")]
    [InlineData("dump-missing")]
    [InlineData("dump-relative")]
    [InlineData("major-missing")]
    [InlineData("major-old")]
    [InlineData("cleanup-missing")]
    [InlineData("cleanup-zero")]
    [InlineData("cleanup-too-large")]
    public void InvalidBackupSettingsFailLocally(string invalid)
    {
        ServiceCollection services = Services(DatabaseProvider.PostgreSql, options =>
        {
            switch (invalid)
            {
                case "directory-missing": options.BackupDirectory = null; break;
                case "directory-relative": options.BackupDirectory = "synthetic-secret-relative"; break;
                case "directory-control": options.BackupDirectory = Path.GetFullPath("synthetic-secret") + "\n"; break;
                case "retention-missing": options.BackupRetentionPeriod = null; break;
                case "retention-zero": options.BackupRetentionPeriod = TimeSpan.Zero; break;
                case "retention-negative": options.BackupRetentionPeriod = TimeSpan.FromTicks(-1); break;
                case "dump-missing": options.PostgreSqlDumpExecutablePath = null; break;
                case "dump-relative": options.PostgreSqlDumpExecutablePath = "synthetic-secret-relative"; break;
                case "major-missing": options.PostgreSqlServerMajorVersion = null; break;
                case "major-old": options.PostgreSqlServerMajorVersion = 9; break;
                case "cleanup-missing": options.PostgreSqlCleanupTimeout = null; break;
                case "cleanup-zero": options.PostgreSqlCleanupTimeout = TimeSpan.Zero; break;
                case "cleanup-too-large": options.PostgreSqlCleanupTimeout = TimeSpan.FromMilliseconds(uint.MaxValue); break;
                default: throw new InvalidOperationException("Неизвестный test case.");
            }
        });
        services.AddScoped<AgentBridgeDbContext>(_ => throw new InvalidOperationException("Context не должен создаваться при ошибке backup options."));
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>());
        Assert.DoesNotContain("synthetic-secret", error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Binding получает ровно раздел backup и не использует host или I/O.</summary>
    [Fact]
    public void ConfigurationBindingUsesExplicitSection()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backup:BackupDirectory"] = Path.GetFullPath("never-created-backups"),
            ["Backup:BackupRetentionPeriod"] = "30.00:00:00",
            ["Backup:PostgreSqlDumpExecutablePath"] = Path.GetFullPath("never-run-pg-dump"),
            ["Backup:PostgreSqlServerMajorVersion"] = "17",
            ["Backup:PostgreSqlCleanupTimeout"] = "00:00:05"
        }).Build();
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.PostgreSql;
            options.ConnectionString = "Host=invalid.example;Database=synthetic";
        });
        services.AddAgentBridgePersistence();

        // Act
        services.AddAgentBridgeDatabaseMaintenance(configuration.GetSection("Backup"));
        using ServiceProvider root = services.BuildServiceProvider();
        root.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        DatabaseBackupOptions backup = root.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value;
        Assert.Equal(Path.GetFullPath("never-created-backups"), backup.BackupDirectory);
        Assert.Equal(TimeSpan.FromDays(30), backup.BackupRetentionPeriod);
        Assert.Equal(Path.GetFullPath("never-run-pg-dump"), backup.PostgreSqlDumpExecutablePath);
        Assert.Equal(17, backup.PostgreSqlServerMajorVersion);
        Assert.Equal(TimeSpan.FromSeconds(5), backup.PostgreSqlCleanupTimeout);
    }

    /// <summary>Проверяет каждый условно обязательный ключ maintenance через configuration без context/DB/process.</summary>
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, "BackupDirectory")]
    [InlineData(DatabaseProvider.PostgreSql, "BackupRetentionPeriod")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSqlDumpExecutablePath")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSqlServerMajorVersion")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSqlCleanupTimeout")]
    public void EachConditionalMaintenanceFieldIsRequired(DatabaseProvider selected, string field)
    {
        foreach (string? value in new string?[] { null, "", " " })
        {
            // Arrange
            IConfigurationRoot config = BackupConfiguration();
            config[field] = value;
            ServiceCollection services = new();
            services.AddTestDatabaseProviders();
            services.Configure<DatabaseBackupOptions>(options =>
            {
                options.BackupDirectory = Path.GetFullPath("prior-backups");
                options.BackupRetentionPeriod = TimeSpan.FromDays(30);
                options.PostgreSqlDumpExecutablePath = Path.GetFullPath("prior-pg-dump");
                options.PostgreSqlServerMajorVersion = 17;
                options.PostgreSqlCleanupTimeout = TimeSpan.FromSeconds(5);
            });
            services.AddDatabaseConfiguration(options =>
            {
                options.Provider = selected;
                options.ConnectionString = "Password=synthetic-secret";
            });

            // Act
            services.AddAgentBridgeDatabaseMaintenance(config);
            using ServiceProvider provider = services.BuildServiceProvider();
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

            // Assert
            Assert.Contains("Backup." + field, error.ToString());
            Assert.DoesNotContain("synthetic-secret", error.ToString());
            Assert.Null(error.InnerException);
        }
    }

    /// <summary>Binder malformed/overflow защищён до standard options даже при синтетическом секрете в значении.</summary>
    [Theory]
    [InlineData("BackupRetentionPeriod", "synthetic-secret")]
    [InlineData("PostgreSqlServerMajorVersion", "synthetic-secret")]
    [InlineData("PostgreSqlServerMajorVersion", "2147483648")]
    [InlineData("PostgreSqlCleanupTimeout", "synthetic-secret")]
    public void MaintenanceBindingErrorsAreSafe(string field, string value)
    {
        // Arrange
        IConfigurationRoot config = BackupConfiguration();
        config[field] = value;
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.PostgreSql;
            options.ConnectionString = "Password=synthetic-secret";
        });
        services.AddTestDatabaseProviders();

        // Act
        services.AddAgentBridgeDatabaseMaintenance(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        Assert.Contains("Backup." + field, error.ToString());
        Assert.DoesNotContain(value, error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Значения принадлежат тесту; пути никогда не открываются и процессы не запускаются.</summary>
    private static IConfigurationRoot BackupConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BackupDirectory"] = Path.GetFullPath("never-created-backups"),
        ["BackupRetentionPeriod"] = "30.00:00:00", ["PostgreSqlDumpExecutablePath"] = Path.GetFullPath("never-run-pg-dump"),
        ["PostgreSqlServerMajorVersion"] = "17", ["PostgreSqlCleanupTimeout"] = "00:00:05"
    }).Build();

    /// <summary>Настраивает production DI только синтетическими значениями без создания файлов.</summary>
    internal static ServiceCollection Services(DatabaseProvider provider, Action<DatabaseBackupOptions>? change = null)
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = provider;
            options.ConnectionString = "Host=127.0.0.1;Port=5432;Database=synthetic;Username=synthetic;Password=synthetic;SSL Mode=Disable";
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            Configure(options);
            change?.Invoke(options);
        });

        return services;
    }

    /// <summary>Синтетическая явная политика для тестов; не является default библиотеки.</summary>
    private static void Configure(DatabaseBackupOptions options)
    {
        options.BackupDirectory = Path.GetFullPath("never-created-backups");
        options.BackupRetentionPeriod = TimeSpan.FromDays(30);
        options.PostgreSqlDumpExecutablePath = Path.GetFullPath("never-run-pg-dump");
        options.PostgreSqlServerMajorVersion = 17;
        options.PostgreSqlCleanupTimeout = TimeSpan.FromSeconds(5);
    }
}
