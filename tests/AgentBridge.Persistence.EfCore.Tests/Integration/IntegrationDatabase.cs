using System.Data.Common;
using System.Text.Json;
using AgentBridge.Configuration;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Backup;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Database;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.PostgreSql.Providers;
using EFCoreLibrary.Maintenance.Sqlite.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Владеет уникальной тестовой БД; все технические операции выполняет фактическая EFCoreLibrary.</summary>
public class IntegrationDatabase : IAsyncDisposable
{
    private readonly List<string> databases = [];
    private readonly string? adminConnection;
    private readonly PostgreSqlIntegrationTools? tools;

    /// <summary>Создаёт только локальную конфигурацию; соединение и схема ещё не открываются.</summary>
    internal IntegrationDatabase(DatabaseProvider provider, string configuredRoot, string? postgresConnection, PostgreSqlIntegrationTools? tools)
    {
        if (provider is not (DatabaseProvider.SQLite or DatabaseProvider.PostgreSql))
        {
            throw new ArgumentOutOfRangeException(nameof(provider));
        }

        Provider = provider;
        this.tools = tools;
        if (!Path.IsPathFullyQualified(configuredRoot) || !Directory.Exists(configuredRoot))
        {
            throw new InvalidOperationException("Требуется существующий абсолютный тестовый каталог.");
        }
        DirectoryPath = Path.Combine(configuredRoot, "abverify_" + Guid.NewGuid().ToString("N"));
        BackupDirectory = Path.Combine(DirectoryPath, "backups");
        if (provider == DatabaseProvider.SQLite)
        {
            ConnectionString = SqliteSettings(Path.Combine(DirectoryPath, "source.db"));
        }
        else
        {
            if (tools is null || postgresConnection is null)
            {
                throw new InvalidOperationException("Требуется PostgreSQL-контейнер и проверенные клиентские утилиты fixture.");
            }

            NpgsqlConnectionStringBuilder configured = new(postgresConnection);
            if (configured.Host != "127.0.0.1" || configured.Database != "postgres" ||
                configured.Username is null || !configured.Username.StartsWith("abverify_", StringComparison.Ordinal) ||
                configured.Port == 5432 || configured.SslMode != SslMode.Disable)
            {
                throw new InvalidOperationException("Разрешён только отдельный loopback PostgreSQL тестового пользователя и порта.");
            }
            configured.Pooling = false;
            adminConnection = configured.ConnectionString;
            configured.Database = NewDatabaseName();
            ConnectionString = configured.ConnectionString;
        }
        Directory.CreateDirectory(BackupDirectory);
        try
        {
            Root = BuildRoot();
        }
        catch
        {
            Directory.Delete(DirectoryPath, true);
            throw;
        }
    }

    /// <summary>Выбранный фактический provider.</summary>
    public DatabaseProvider Provider { get; }
    /// <summary>Каталог только этого fixture.</summary>
    public string DirectoryPath { get; }
    /// <summary>Явный каталог реальных backup.</summary>
    public string BackupDirectory { get; }
    /// <summary>Подключение только к уникальной тестовой БД; не писать в логи.</summary>
    public string ConnectionString { get; }
    /// <summary>Root DI container с настоящей persistence/maintenance цепочкой.</summary>
    public ServiceProvider Root { get; }

    /// <summary>Утилиты доступны только PostgreSQL; нарушение конфигурации не скрывается nullable suppression.</summary>
    private PostgreSqlIntegrationTools PostgreSqlTools => tools
        ?? throw new InvalidOperationException("Утилиты PostgreSQL не подготовлены fixture.");

    /// <summary>Регистрирует существующие production API; разрешает адресную тестовую настройку отказов.</summary>
    public ServiceProvider BuildRoot(Action<DatabaseBackupOptions>? backup = null, Action<ServiceCollection>? customize = null,
        string? connection = null)
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddLogging();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = Provider;
            options.ConnectionString = connection ?? ConnectionString;
        });
        services.AddAgentBridgePersistence();
        services.Configure<DialogRetentionOptions>(options => options.RetentionPeriod = TimeSpan.FromHours(36));
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            options.BackupDirectory = BackupDirectory;
            options.BackupRetentionPeriod = TimeSpan.FromDays(1);
            if (Provider == DatabaseProvider.PostgreSql)
            {
                options.PostgreSqlDumpExecutablePath = PostgreSqlTools.DumpPath;
                options.PostgreSqlServerMajorVersion = PostgreSqlIntegrationTools.SERVER_MAJOR;
                options.PostgreSqlCleanupTimeout = TimeSpan.FromSeconds(10);
            }
            backup?.Invoke(options);
        }, MaintenanceExecutionMode.SingleInitializer);
        customize?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Вызывает настоящую первую установку без фиктивного backup.</summary>
    public async Task InitializeAsync()
    {
        using IServiceScope scope = Root.CreateScope();
        DatabaseMaintenanceResult result = await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
            .InitializeNewAsync(TimeSpan.FromSeconds(45));
        Assert.Equal(MaintenanceOutcome.Initialized, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Equal(3, result.AppliedMigrations.Count);
    }

    /// <summary>Создаёт пустую тестовую БД настоящим provider, не применяя migration.</summary>
    public async Task CreateEmptyAsync()
    {
        using IServiceScope scope = Root.CreateScope();
        using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
        await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>().CreateAsync(budget);
    }

    /// <summary>Исполняет только разрешённый технический SQL теста через библиотечный command API.</summary>
    public async Task ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using IServiceScope scope = Root.CreateScope();
        using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
        IRelationalMigrationOperations<AgentBridgeContextKey> migrations = scope.ServiceProvider.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        await scope.ServiceProvider.GetRequiredService<IDatabaseCommands>().ExecuteAsync(migrations.Connection, sql,
            parameters ?? new Dictionary<string, object?>(), budget);
    }

    /// <summary>Наблюдает сохранённую схему/данные, не подменяя production операции.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string sql, string? connection = null)
    {
        using IServiceScope scope = Root.CreateScope();
        using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
        await using DbConnection target = OpenConnection(connection ?? ConnectionString);
        return await scope.ServiceProvider.GetRequiredService<IDatabaseCommands>().QueryAsync(target, sql,
            new Dictionary<string, object?>(), budget);
    }

    /// <summary>Сопоставляет каталоги таблиц, колонок, индексов, ограничений и данные всех таблиц между source/restore.</summary>
    public async Task<string> FingerprintAsync(string? connection = null)
    {
        string schemaSql = Provider == DatabaseProvider.SQLite
            ? "SELECT type, name, tbl_name, sql FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' ORDER BY type, name"
            : "SELECT 'column' AS kind, table_name AS parent, column_name AS name, data_type || ':' || is_nullable || ':' || coalesce(collation_name, '') AS definition FROM information_schema.columns WHERE table_schema='public' UNION ALL SELECT 'index', tablename, indexname, indexdef FROM pg_indexes WHERE schemaname='public' UNION ALL SELECT 'constraint', c.relname, con.conname, pg_get_constraintdef(con.oid) FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' ORDER BY kind, parent, name";
        List<string> parts = [JsonSerializer.Serialize(await QueryAsync(schemaSql, connection))];
        string tablesSql = Provider == DatabaseProvider.SQLite
            ? "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name"
            : "SELECT table_name AS name FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE' ORDER BY table_name";
        foreach (IReadOnlyDictionary<string, object?> table in await QueryAsync(tablesSql, connection))
        {
            string name = (string)table["name"]!;
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = await QueryAsync("SELECT * FROM \"" + name.Replace("\"", "\"\"") + "\"", connection);
            parts.Add(name + ":" + string.Join("\n", rows.Select(row => JsonSerializer.Serialize(row.OrderBy(pair => pair.Key)))
                .Order(StringComparer.Ordinal)));
        }
        return string.Join("\n", parts);
    }

    /// <summary>Восстанавливает реальный артефакт в отдельную тестовую БД через native API либо штатный pg_restore.</summary>
    public async Task<string> RestoreAsync(LocalBackupArtifact artifact, bool expectSuccess = true)
    {
        if (Provider == DatabaseProvider.SQLite)
        {
            string restoredPath = Path.Combine(DirectoryPath, "restored_" + Guid.NewGuid().ToString("N") + ".db");
            using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
            await SqliteBackupStepper.CopyAsync(new SqliteBackupApi().Open(artifact.Path, restoredPath), budget);
            return SqliteSettings(restoredPath);
        }
        NpgsqlConnectionStringBuilder restored = new(adminConnection!) { Database = NewDatabaseName() };
        await using (NpgsqlConnection admin = new(adminConnection))
        {
            using IServiceScope scope = Root.CreateScope();
            using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
            await scope.ServiceProvider.GetRequiredService<IDatabaseCommands>().ExecuteAsync(admin,
                "CREATE DATABASE \"" + restored.Database + "\"", new Dictionary<string, object?>(), budget);
        }
        using IServiceScope restoreScope = Root.CreateScope();
        using BackupWorkspace workspace = new(BackupDirectory, "restore");
        string credentials = await workspace.WriteSecretAsync(restored.Host + ":" + restored.Port + ":" + restored.Database + ":" +
            restored.Username + ":" + restored.Password + "\n", default);
        using MaintenanceBudget restoreBudget = new(TimeSpan.FromSeconds(45), default);
        ProcessResult result = await restoreScope.ServiceProvider.GetRequiredService<IBackupProcessRunner>().RunAsync(
            new ProcessCommand(PostgreSqlTools.RestorePath,
                ["--no-password", "--exit-on-error", "--dbname=" + PostgreSqlMaintenanceProvider<AgentBridgeContextKey>.BuildConnectionInfo(restored), artifact.Path],
                new Dictionary<string, string> { ["PGPASSFILE"] = credentials, ["LC_ALL"] = "C" }, workspace.RecoverAfterConfirmedStop),
            restoreBudget, TimeSpan.FromSeconds(10));
        Assert.Equal(expectSuccess, result.ExitCode == 0);
        return restored.ConnectionString;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        List<Exception> errors = [];
        try { await Root.DisposeAsync(); }
        catch (Exception exception) { errors.Add(exception); }

        if (adminConnection is not null)
        {
            DatabaseCommands commands = new();
            foreach (string database in databases)
            {
                if (!database.StartsWith("abverify_", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Отказ удаления чужой БД.");
                }
                try
                {
                    await using NpgsqlConnection admin = new(adminConnection);
                    using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
                    await commands.ExecuteAsync(admin, "DROP DATABASE IF EXISTS \"" + database + "\" WITH (FORCE)",
                        new Dictionary<string, object?>(), budget);
                }
                catch (Exception exception) { errors.Add(exception); }
            }
        }
        // Unconfirmed disposal может означать ещё работающий backup process: его файлы сохраняются.
        if (errors.Count == 0)
        {
            Directory.Delete(DirectoryPath, true);
        }
        else
        {
            throw new AggregateException("Не удалось очистить собственную тестовую БД.", errors);
        }
    }

    /// <summary>Формирует локальное непулируемое подключение с включёнными FK.</summary>
    private static string SqliteSettings(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path, Pooling = false, ForeignKeys = true, DefaultTimeout = 2
    }.ConnectionString;

    /// <summary>Создаёт и сразу регистрирует собственное имя для cleanup даже после отказа CREATE.</summary>
    private string NewDatabaseName()
    {
        string name = "abverify_" + Guid.NewGuid().ToString("N");
        databases.Add(name);
        return name;
    }

    /// <summary>Создаёт connection выбранного provider только для библиотечных commands.</summary>
    private DbConnection OpenConnection(string connection) => Provider == DatabaseProvider.SQLite
        ? new SqliteConnection(connection) : new NpgsqlConnection(connection);

}
