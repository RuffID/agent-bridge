using System.Text.Json;
using AgentBridge.Configuration;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.SqlServer.Providers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Владеет DI для собственной БД в контейнере коллекции; операции выполняет AgentBridge/EFCoreLibrary.</summary>
public class SqlServerIntegrationDatabase : IAsyncDisposable
{
    private readonly SqlServerIntegrationSettings settings;

    /// <summary>Принимает проверенные ресурсы fixture и создаёт DI без открытия соединения.</summary>
    internal SqlServerIntegrationDatabase(SqlServerIntegrationSettings settings)
    {
        this.settings = settings;
        Root = BuildRoot();
    }

    /// <summary>Root gate сохраняется между maintenance entrant одного fixture.</summary>
    public ServiceProvider Root { get; }

    /// <summary>Создаёт новый DI root для чтения либо подстановки отказа; SQL Server не перезапускается.</summary>
    public ServiceProvider BuildRoot(Action<ServiceCollection>? customize = null)
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddLogging();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = settings.ConnectionString;
        });
        services.AddAgentBridgePersistence();
        services.Configure<DialogRetentionOptions>(options => options.RetentionPeriod = TimeSpan.FromHours(1));
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            options.SqlServerBackupDirectory = settings.ServerBackupDirectory;
            options.BackupRetentionPeriod = TimeSpan.FromDays(1);
        }, MaintenanceExecutionMode.SingleInitializer);
        customize?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Явно создаёт собственную БД и применяет миграции настоящим maintenance API.</summary>
    public async Task<DatabaseMaintenanceResult> InitializeNewAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = Root.CreateScope();
        DatabaseMaintenanceResult result = await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
            .InitializeNewAsync(settings.OperationBudget, cancellationToken);

        Assert.Equal(MaintenanceOutcome.Initialized, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Equal(IntegrationSchemaExpectations.Migrations(DatabaseProvider.SqlServer), result.AppliedMigrations);

        return result;
    }

    /// <summary>Создаёт собственную пустую БД через настоящий provider без применения migration.</summary>
    public async Task CreateEmptyAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = Root.CreateScope();
        using MaintenanceBudget budget = new(settings.OperationBudget, cancellationToken);

        await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>().CreateAsync(budget);
    }

    /// <summary>Выполняет технический SQL через command API библиотеки; бизнес-записи остаются в UoW.</summary>
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? parameters = null, bool master = false)
    {
        using IServiceScope scope = Root.CreateScope();
        using MaintenanceBudget budget = new(settings.OperationBudget, cancellationToken);
        SqlConnectionStringBuilder connection = new(settings.ConnectionString);
        if (master) { connection.InitialCatalog = "master"; }
        await using SqlConnection target = new(connection.ConnectionString);

        await scope.ServiceProvider.GetRequiredService<IDatabaseCommands>().ExecuteAsync(target, sql,
            parameters ?? new Dictionary<string, object?>(), budget);
    }

    /// <summary>Читает фактическую схему/данные через техническую command-границу библиотеки.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string sql, CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? parameters = null, bool master = false)
    {
        using IServiceScope scope = Root.CreateScope();
        using MaintenanceBudget budget = new(settings.OperationBudget, cancellationToken);
        SqlConnectionStringBuilder connection = new(settings.ConnectionString);
        if (master) { connection.InitialCatalog = "master"; }
        await using SqlConnection target = new(connection.ConnectionString);

        return await scope.ServiceProvider.GetRequiredService<IDatabaseCommands>().QueryAsync(target, sql,
            parameters ?? new Dictionary<string, object?>(), budget);
    }

    /// <summary>Сопоставляет таблицы, колонки, индексы, defaults/check/FK и все данные без нестабильных object ID.</summary>
    public async Task<string> FingerprintAsync(CancellationToken cancellationToken)
    {
        string[] catalogs =
        [
            "SELECT t.name AS [table], c.name AS [column], ty.name AS [type], c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity, c.collation_name, dc.definition AS [default], cc.definition AS computed FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id WHERE SCHEMA_NAME(t.schema_id)='dbo' ORDER BY t.name,c.column_id",
            "SELECT t.name AS [table], i.name, i.type_desc, i.is_unique, i.is_primary_key, i.is_unique_constraint, i.filter_definition, c.name AS [column], ic.key_ordinal, ic.is_descending_key, ic.is_included_column FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id LEFT JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id LEFT JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE SCHEMA_NAME(t.schema_id)='dbo' ORDER BY t.name,i.name,ic.index_column_id",
            "SELECT t.name AS [table], ck.name, ck.definition, ck.is_disabled, ck.is_not_trusted FROM sys.tables t JOIN sys.check_constraints ck ON ck.parent_object_id=t.object_id WHERE SCHEMA_NAME(t.schema_id)='dbo' ORDER BY t.name,ck.name",
            "SELECT t.name AS [table], fk.name, pc.name AS [column], rt.name AS referenced_table, rc.name AS referenced_column, fk.delete_referential_action_desc, fk.update_referential_action_desc, fk.is_disabled, fk.is_not_trusted FROM sys.tables t JOIN sys.foreign_keys fk ON fk.parent_object_id=t.object_id JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id JOIN sys.columns pc ON pc.object_id=t.object_id AND pc.column_id=fc.parent_column_id JOIN sys.tables rt ON rt.object_id=fk.referenced_object_id JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fc.referenced_column_id WHERE SCHEMA_NAME(t.schema_id)='dbo' ORDER BY t.name,fk.name,fc.constraint_column_id"
        ];
        List<string> parts = [];
        foreach (string catalog in catalogs)
        {
            parts.Add(JsonSerializer.Serialize(await QueryAsync(catalog, cancellationToken)));
        }
        foreach (IReadOnlyDictionary<string, object?> table in await QueryAsync(
            "SELECT name FROM sys.tables WHERE SCHEMA_NAME(schema_id)='dbo' ORDER BY name", cancellationToken))
        {
            string name = (string)table["name"]!;
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = await QueryAsync("SELECT * FROM [dbo]." + QuoteIdentifier(name), cancellationToken);
            parts.Add(name + ":" + string.Join("\n", rows.Select(row => JsonSerializer.Serialize(row.OrderBy(pair => pair.Key)))
                .Order(StringComparer.Ordinal)));
        }

        return string.Join("\n", parts);
    }

    /// <summary>Восстанавливает настоящий backup set в новую собственную БД через command API библиотеки.</summary>
    public async Task<SqlServerIntegrationDatabase> RestoreAsync(ServerBackupArtifact artifact, CancellationToken cancellationToken)
    {
        if (!artifact.IsConfirmed || !artifact.Locator.StartsWith(settings.ServerBackupDirectory + "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Backup должен быть подтверждён и принадлежать собственному контейнеру.");
        }
        string name = "abverify_" + Guid.NewGuid().ToString("N");
        Dictionary<string, object?> parameters = new() { ["@path"] = artifact.Locator, ["@position"] = artifact.Position };
        IReadOnlyList<IReadOnlyDictionary<string, object?>> files = await QueryAsync(
            "RESTORE FILELISTONLY FROM DISK=@path WITH FILE=@position", cancellationToken, parameters, master: true);
        List<string> moves = [];
        foreach (IReadOnlyDictionary<string, object?> file in files)
        {
            string type = (string)file["Type"]!;
            if (type is not ("D" or "L")) { throw new InvalidOperationException("Неожиданный тип backup-файла."); }
            string destination = "/var/opt/mssql/data/" + name + "_" + moves.Count + (type == "D" ? ".mdf" : ".ldf");
            moves.Add("MOVE " + QuoteText((string)file["LogicalName"]!) + " TO " + QuoteText(destination));
        }
        if (moves.Count < 2) { throw new InvalidOperationException("Backup не содержит data/log."); }

        await ExecuteAsync("RESTORE DATABASE " + QuoteIdentifier(name) + " FROM DISK=@path WITH FILE=@position, CHECKSUM, RECOVERY, " +
            string.Join(", ", moves), cancellationToken, parameters, master: true);
        SqlConnectionStringBuilder restored = new(settings.ConnectionString) { InitialCatalog = name };

        return new(new(restored.ConnectionString, restored.DataSource, name, settings.ServerBackupDirectory, settings.OperationBudget));
    }

    /// <summary>После отказа migration находит реально записанный backup, повторно проверяя header/checksum сервером.</summary>
    public async Task<ServerBackupArtifact> ReadBackupAfterFailureAsync(CancellationToken cancellationToken)
    {
        SqlConnectionStringBuilder connection = new(settings.ConnectionString);
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = await QueryAsync(
            "SELECT b.name, m.physical_device_name FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON m.media_set_id=b.media_set_id WHERE b.database_name=@database AND b.is_copy_only=1 AND b.type='D'",
            cancellationToken, new Dictionary<string, object?> { ["@database"] = connection.InitialCatalog }, master: true);
        if (rows.Count != 1) { throw new InvalidOperationException("Требуется единственный настоящий backup тестовой БД."); }
        string locator = (string)rows[0]["physical_device_name"]!;
        Dictionary<string, object?> parameters = new() { ["@path"] = locator };
        ServerBackupArtifact artifact = SqlServerMaintenanceProvider<AgentBridgeContextKey>.ConfirmHeader(
            await QueryAsync("RESTORE HEADERONLY FROM DISK=@path", cancellationToken, parameters, master: true),
            connection.InitialCatalog, (string)rows[0]["name"]!, locator);
        parameters["@position"] = artifact.Position;
        await ExecuteAsync("RESTORE VERIFYONLY FROM DISK=@path WITH FILE=@position, CHECKSUM", cancellationToken, parameters, master: true);

        return artifact with { VerifyOnlySucceeded = true };
    }

    /// <summary>Экранирует единственный SQL identifier технического запроса.</summary>
    private static string QuoteIdentifier(string value) => "[" + value.Replace("]", "]]") + "]";
    /// <summary>Экранирует строку server metadata в технической restore-команде.</summary>
    private static string QuoteText(string value) => "N'" + value.Replace("'", "''") + "'";

    /// <inheritdoc/>
    /// <remarks>Освобождает DI; БД и серверные файлы удаляются вместе с собственным контейнером коллекции.</remarks>
    public ValueTask DisposeAsync() => Root.DisposeAsync();
}
