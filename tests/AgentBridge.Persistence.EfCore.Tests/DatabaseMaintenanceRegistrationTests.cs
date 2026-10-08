using System.Data;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.PostgreSql.Providers;
using EFCoreLibrary.Maintenance.Sqlite.Providers;
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
    [InlineData(DatabaseProvider.SQLite, "sqlite", "sqlite database", "file main only", false)]
    [InlineData(DatabaseProvider.PostgreSql, "postgresql", "custom", "selected database; excludes global roles", true)]
    public void RegistrationAndResolutionDoNotStartMaintenance(DatabaseProvider selected, string name, string format, string scopeName, bool external)
    {
        ServiceCollection services = Services(selected);
        using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        root.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = first.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
        Assert.IsType<DatabaseMaintenance<AgentBridgeContextKey>>(maintenance);
        Assert.Equal(new DatabaseMaintenanceCapabilities(name, format, scopeName, external, false), maintenance.Capabilities);
        object provider = first.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
        Assert.Equal(selected == DatabaseProvider.SQLite ? typeof(SqliteMaintenanceProvider<AgentBridgeContextKey>) : typeof(PostgreSqlMaintenanceProvider<AgentBridgeContextKey>), provider.GetType());
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
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.SQLite; options.ConnectionString = "Data Source=never-open.db"; });
        services.AddAgentBridgePersistence();
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDatabaseMaintenance<AgentBridgeContextKey>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(SingleInitializerGate));
    }

    /// <summary>Неподдержанный режим отклоняется до изменения DI.</summary>
    [Fact]
    public void ExecutionModeMustBeExplicit()
    {
        ServiceCollection services = new();
        MaintenanceException error = Assert.Throws<MaintenanceException>(() => services.AddAgentBridgeDatabaseMaintenance(Configure,
            (MaintenanceExecutionMode)123));
        Assert.Equal(MaintenanceError.Configuration, error.Code);
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

    /// <summary>Срок хранения не берётся из expiry диалогов; SQLite не требует pg_dump.</summary>
    [Fact]
    public void SqliteRequiresExplicitRetentionButNoPostgreSqlFields()
    {
        ServiceCollection services = Services(DatabaseProvider.SQLite, options =>
        {
            options.PostgreSqlDumpExecutablePath = null;
            options.PostgreSqlServerMajorVersion = null;
            options.PostgreSqlCleanupTimeout = null;
            options.BackupRetentionPeriod = TimeSpan.FromTicks(1);
        });
        using ServiceProvider root = services.BuildServiceProvider();
        root.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(TimeSpan.FromTicks(1), root.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value.BackupRetentionPeriod);
        Assert.Null(new DatabaseBackupOptions().BackupRetentionPeriod);
    }

    /// <summary>Binding получает ровно раздел backup и не использует host или I/O.</summary>
    [Fact]
    public void ConfigurationBindingUsesExplicitSection()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backup:BackupDirectory"] = Path.GetFullPath("never-created-backups"),
            ["Backup:BackupRetentionPeriod"] = "30.00:00:00"
        }).Build();
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.SQLite; options.ConnectionString = "Data Source=never-open.db"; });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(configuration.GetSection("Backup"), MaintenanceExecutionMode.SingleInitializer);
        using ServiceProvider root = services.BuildServiceProvider();
        root.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(TimeSpan.FromDays(30), root.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value.BackupRetentionPeriod);
    }

    /// <summary>Проверяет каждый условно обязательный ключ maintenance через configuration без context/DB/process.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite, "BackupDirectory")]
    [InlineData(DatabaseProvider.SQLite, "BackupRetentionPeriod")]
    [InlineData(DatabaseProvider.PostgreSql, "BackupDirectory")]
    [InlineData(DatabaseProvider.PostgreSql, "BackupRetentionPeriod")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSqlDumpExecutablePath")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSqlServerMajorVersion")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSqlCleanupTimeout")]
    [InlineData(DatabaseProvider.SqlServer, "SqlServerBackupDirectory")]
    [InlineData(DatabaseProvider.SqlServer, "BackupRetentionPeriod")]
    public void EachConditionalMaintenanceFieldIsRequired(DatabaseProvider selected, string field)
    {
        foreach (string? value in new string?[] { null, "", " " })
        {
            IConfigurationRoot config = BackupConfiguration(); config[field] = value;
            ServiceCollection services = new();
            services.AddTestDatabaseProviders();
            services.Configure<DatabaseBackupOptions>(options =>
            {
                options.BackupDirectory = Path.GetFullPath("prior-backups"); options.SqlServerBackupDirectory = "/prior-backups";
                options.BackupRetentionPeriod = TimeSpan.FromDays(30); options.PostgreSqlDumpExecutablePath = Path.GetFullPath("prior-pg-dump");
                options.PostgreSqlServerMajorVersion = 17; options.PostgreSqlCleanupTimeout = TimeSpan.FromSeconds(5);
            });
            services.AddDatabaseConfiguration(options => { options.Provider = selected; options.ConnectionString = "Password=synthetic-secret"; });
            services.AddAgentBridgeDatabaseMaintenance(config, MaintenanceExecutionMode.SingleInitializer);
            using ServiceProvider provider = services.BuildServiceProvider();
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
            Assert.Contains("Backup." + field, error.ToString()); Assert.DoesNotContain("synthetic-secret", error.ToString()); Assert.Null(error.InnerException);
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
        IConfigurationRoot config = BackupConfiguration(); config[field] = value;
        ServiceCollection services = new(); services.AddDatabaseConfiguration(options => { options.Provider = DatabaseProvider.PostgreSql; options.ConnectionString = "Password=synthetic-secret"; });
        services.AddTestDatabaseProviders();
        services.AddAgentBridgeDatabaseMaintenance(config, MaintenanceExecutionMode.SingleInitializer);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("Backup." + field, error.ToString()); Assert.DoesNotContain(value, error.ToString()); Assert.Null(error.InnerException);
    }

    /// <summary>Значения принадлежат тесту; пути никогда не открываются и процессы не запускаются.</summary>
    private static IConfigurationRoot BackupConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BackupDirectory"] = Path.GetFullPath("never-created-backups"), ["SqlServerBackupDirectory"] = "/server/backups",
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
            options.ConnectionString = provider == DatabaseProvider.SQLite ? "Data Source=never-open.db"
                : "Host=127.0.0.1;Port=5432;Database=synthetic;Username=synthetic;Password=synthetic;SSL Mode=Disable";
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(options => { Configure(options); change?.Invoke(options); }, MaintenanceExecutionMode.SingleInitializer);
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
