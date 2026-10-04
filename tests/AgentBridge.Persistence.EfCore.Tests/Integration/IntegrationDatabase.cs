using System.Data.Common;
using System.Text.Json;
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

    /// <summary>Создаёт только локальную конфигурацию; соединение и схема ещё не открываются.</summary>
    public IntegrationDatabase(DatabaseProvider provider)
    {
        Provider = provider;
        string configuredRoot = Required("AGENTBRIDGE_INTEGRATION_ROOT");
        if (!Path.IsPathFullyQualified(configuredRoot) || !Directory.Exists(configuredRoot))
        {
            throw new InvalidOperationException("Требуется существующий абсолютный тестовый каталог.");
        }
        DirectoryPath = Path.Combine(configuredRoot, "abverify_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        BackupDirectory = Path.Combine(DirectoryPath, "backups");
        Directory.CreateDirectory(BackupDirectory);
        if (provider == DatabaseProvider.SQLite)
        {
            ConnectionString = SqliteSettings(Path.Combine(DirectoryPath, "source.db"));
        }
        else
        {
            NpgsqlConnectionStringBuilder configured = new(Required("AGENTBRIDGE_POSTGRES_CONNECTION"));
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
        Root = BuildRoot();
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

    /// <summary>Регистрирует существующие production API; разрешает адресную тестовую настройку отказов.</summary>
    public ServiceProvider BuildRoot(Action<DatabaseBackupOptions>? backup = null, Action<ServiceCollection>? customize = null,
        string? connection = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = Provider;
            options.ConnectionString = connection ?? ConnectionString;
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            options.BackupDirectory = BackupDirectory;
            options.BackupRetentionPeriod = TimeSpan.FromDays(1);
            if (Provider == DatabaseProvider.PostgreSql)
            {
                options.PostgreSqlDumpExecutablePath = Required("AGENTBRIDGE_PG_DUMP");
                options.PostgreSqlServerMajorVersion = int.Parse(Required("AGENTBRIDGE_PG_MAJOR"));
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
            new ProcessCommand(Required("AGENTBRIDGE_PG_RESTORE"),
                ["--no-password", "--exit-on-error", "--dbname=" + PostgreSqlMaintenanceProvider<AgentBridgeContextKey>.BuildConnectionInfo(restored), artifact.Path],
                new Dictionary<string, string> { ["PGPASSFILE"] = credentials, ["LC_ALL"] = "C" }, workspace.RecoverAfterConfirmedStop),
            restoreBudget, TimeSpan.FromSeconds(10));
        Assert.Equal(expectSuccess, result.ExitCode == 0);
        return restored.ConnectionString;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Root.DisposeAsync();
        if (adminConnection is not null)
        {
            DatabaseCommands commands = new();
            foreach (string database in databases)
            {
                if (!database.StartsWith("abverify_", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Отказ удаления чужой БД.");
                }
                await using NpgsqlConnection admin = new(adminConnection);
                using MaintenanceBudget budget = new(TimeSpan.FromSeconds(30), default);
                await commands.ExecuteAsync(admin, "DROP DATABASE IF EXISTS \"" + database + "\" WITH (FORCE)",
                    new Dictionary<string, object?>(), budget);
            }
        }
        Directory.Delete(DirectoryPath, true);
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

    /// <summary>Не допускает незаметного пропуска включённой проверки.</summary>
    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Отсутствует обязательная интеграционная настройка " + name + ".");
}
