using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Проверяет настоящие MSSQL migration/append/context и новый DI root в собственном контейнере.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class SqlServerPersistenceIntegrationTests(DatabaseIntegrationFixture environment)
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From(" User:Б ");
    // Legacy fixtures наполняют только эти таблицы; catalog/feed не входят в root cascade.
    private static readonly string[] LEGACY_DIALOG_TABLES = ["Dialogs", "DialogTurns", "ModelSteps", "CanonicalItems", "DialogContexts", "DialogSettings"];

    /// <summary>Два настоящих Serializable writer одного root не фиксируются вместе; данные принадлежат победителю.</summary>
    [SqlServerIntegrationFact]
    public async Task SimultaneousWritersCannotBothCommit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        await database.InitializeNewAsync(ct);
        DialogWriteToken token = await CreateAsync(database, ct);
        ConcurrentReadBarrier barrier = new();
        await using ServiceProvider root = database.BuildRoot(services =>
            services.AddDbContext<AgentBridgeDbContext>(options => options.AddInterceptors(barrier)));
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        Guid leftTurn = Guid.NewGuid();
        Guid rightTurn = Guid.NewGuid();
        Task<(ServiceResult<DialogWriteToken>? Result, Exception? Error)> left = AttemptAsync(first, leftTurn, "left");
        Task<(ServiceResult<DialogWriteToken>? Result, Exception? Error)> right = AttemptAsync(second, rightTurn, "right");

        (ServiceResult<DialogWriteToken>? Result, Exception? Error)[] outcomes = await Task.WhenAll(left, right);

        (ServiceResult<DialogWriteToken>? Result, Exception? Error) winner = Assert.Single(outcomes, outcome => outcome.Result?.Success == true);
        Assert.Null(winner.Error);
        (ServiceResult<DialogWriteToken>? Result, Exception? Error) loser = Assert.Single(outcomes, outcome => outcome.Result?.Success != true);
        if (loser.Error is not null)
        {
            // Только действительный deadlock victim; timeout, constraint, cleanup и arbitrary exception не считаются concurrency evidence.
            // SqlServerExecutionStrategy без retries оборачивает transient driver failure в InvalidOperationException.
            SqlException? driver = loser.Error switch
            {
                SqlException sql => sql,
                DbUpdateException { InnerException: SqlException sql } => sql,
                InvalidOperationException { InnerException: SqlException sql } => sql,
                InvalidOperationException { InnerException: DbUpdateException { InnerException: SqlException sql } } => sql,
                _ => null
            };
            Assert.True(driver is not null, loser.Error.ToString());
            Assert.Equal(1205, driver!.Number);
            Assert.Null(loser.Result);
        }
        else
        {
            Assert.False(loser.Result!.Success);
            Assert.Null(loser.Result.Data);
            Assert.Equal(ServiceErrorType.Conflict, loser.Result.Error!.Type);
        }
        DialogSnapshot final = await ReadAsync(database, token, ct);
        StoredDialogTurn turn = Assert.Single(final.Turns);
        bool leftWon = outcomes[0].Result?.Success == true;
        Assert.Equal(leftWon ? leftTurn : rightTurn, turn.Id);
        Assert.Equal(leftWon ? "left" : "right", Assert.Single(turn.Items).Content.GetProperty("text").GetString());
        Assert.Equal(token.Revision + 1, final.Token.Revision);
        Assert.Equal(winner.Result!.Data!.Revision, final.Token.Revision);
        Assert.Empty(turn.ModelSteps);
        Assert.Null(final.ActiveContext);
        Assert.Empty(first.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        Assert.Empty(second.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        Assert.Single(await database.QueryAsync("SELECT * FROM [DialogTurns]", ct));
        Assert.Single(await database.QueryAsync("SELECT * FROM [CanonicalItems]", ct));

        async Task<(ServiceResult<DialogWriteToken>?, Exception?)> AttemptAsync(IServiceScope scope, Guid turnId, string text)
        {
            try
            {
                return (await scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>().BeginAsync(Access(token), token, turnId,
                    [PersistenceIntegrationTests.Item(JsonSerializer.Serialize(new { text }))], ct), null);
            }
            catch (Exception error) { return (null, error); }
        }
    }

    /// <summary>Отказ после реального SQL SaveChanges до commit откатывает root revision и всех staged детей.</summary>
    [SqlServerIntegrationFact]
    public async Task FailureAfterRealSaveRollsBackRevisionAndChildren()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        await database.InitializeNewAsync(ct);
        DialogWriteToken token = await CreateAsync(database, ct);
        Guid turn = Guid.NewGuid();
        using (IServiceScope setup = database.Root.CreateScope())
        {
            token = PersistenceIntegrationTests.Success(await setup.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                .BeginAsync(Access(token), token, turn, [PersistenceIntegrationTests.Item("{\"text\":\"before\"}")], ct));
        }
        string before = await database.FingerprintAsync(ct);
        await using ServiceProvider failureRoot = database.BuildRoot(services => services.AddScoped<IUnitOfWorkSession, ThrowAfterSaveSession>());
        using IServiceScope scope = failureRoot.CreateScope();
        ModelResponse response = PersistenceIntegrationTests.Response(ModelResponseStatus.Completed);

        IOException failure = await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
            .FinishAsync(Access(token), token, turn, DialogTurnStatus.Completed, response.Output, [new(Guid.NewGuid(), response)], ct));

        Assert.Equal("Тестовый отказ после реального SaveChanges до commit.", failure.Message);
        Assert.True(((ThrowAfterSaveSession)scope.ServiceProvider.GetRequiredService<IUnitOfWorkSession>()).SavedEntries >= 4);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        Assert.Equal(before, await database.FingerprintAsync(ct));
        DialogSnapshot final = await ReadAsync(database, token, ct);
        Assert.Equal(token.Revision, final.Token.Revision);
        Assert.Equal(DialogTurnStatus.InProgress, Assert.Single(final.Turns).Status);
        Assert.Single(final.Turns[0].Items);
        Assert.Empty(final.Turns[0].ModelSteps);
    }

    /// <summary>Root delete каскадно удаляет данные шести legacy-таблиц и сохраняет соседний наполненный диалог.</summary>
    [SqlServerIntegrationFact]
    public async Task DeleteCascadesSixTablesAndPreservesNeighbor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        await database.InitializeNewAsync(ct);
        DialogWriteToken token = await FillAsync(database, await CreateAsync(database, ct), "delete", ct);
        DialogWriteToken neighbor = await FillAsync(database, await CreateAsync(database, ct), "neighbor 😀", ct);
        Dictionary<string, string> before = [];
        foreach (string table in LEGACY_DIALOG_TABLES)
        {
            Assert.Single(await RowsAsync(database, table, token, ct));
            before.Add(table, JsonSerializer.Serialize(await RowsAsync(database, table, neighbor, ct)));
        }
        using (IServiceScope scope = database.Root.CreateScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IDialogDeletion>().DeleteAsync(Access(token), token, ct)).Success);
        }

        foreach (string table in LEGACY_DIALOG_TABLES)
        {
            Assert.Empty(await RowsAsync(database, table, token, ct));
            Assert.Equal(before[table], JsonSerializer.Serialize(await RowsAsync(database, table, neighbor, ct)));
        }
        Assert.Equal(neighbor.Revision, (await ReadAsync(database, neighbor, ct)).Token.Revision);
        using IServiceScope read = database.Root.CreateScope();
        Assert.Equal(ServiceErrorType.NotFound, (await read.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(token), ct)).Error!.Type);
    }

    /// <summary>UpdateExisting делает настоящий backup существующей БД до DDL; restore сохраняет именно исходную схему/данные.</summary>
    [SqlServerIntegrationFact]
    public async Task ExistingDatabaseBackupPrecedesMigrationAndRestoresOriginal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        await database.CreateEmptyAsync(ct);
        await SeedSentinelAsync(database, ct);
        string before = await database.FingerprintAsync(ct);
        ServerBackupArtifact artifact;
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
            DatabaseMaintenanceResult result = await maintenance.UpdateExistingAsync(TimeSpan.FromMinutes(2), ct);

            Assert.Equal(MaintenanceOutcome.Migrated, result.Outcome);
            Assert.Equal(IntegrationSchemaExpectations.Migrations(DatabaseProvider.SqlServer), result.AppliedMigrations);
            Assert.NotNull(result.Backup);
            Assert.True(result.Backup.Confirms(result.Backup.OperationId, result.Backup.TargetIdentity, maintenance.Capabilities, result.Backup.StartedAtUtc));
            artifact = Assert.IsType<ServerBackupArtifact>(result.Backup.Artifact);
            Assert.True(artifact.IsConfirmed);
        }
        await using SqlServerIntegrationDatabase restored = await database.RestoreAsync(artifact, ct);

        Assert.Equal(before, await restored.FingerprintAsync(ct));
        Assert.NotEqual(before, await database.FingerprintAsync(ct));
        Assert.Equal("сохранено 😀", Assert.Single(await database.QueryAsync("SELECT * FROM [Sentinel]", ct))["Text"]);
        Assert.Equal("сохранено 😀", Assert.Single(await restored.QueryAsync("SELECT * FROM [Sentinel]", ct))["Text"]);
        Assert.Empty(await restored.QueryAsync("SELECT name FROM sys.tables WHERE name='Dialogs'", ct));
    }

    /// <summary>Настоящий native backup/restore сохраняет текущую схему всех таблиц и canonical payload legacy-диалога.</summary>
    [SqlServerIntegrationFact]
    public async Task FullBackupRestoresCurrentSchemaAndLegacyData()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        await database.InitializeNewAsync(ct);
        DialogWriteToken token = await FillAsync(database, await CreateAsync(database, ct), "backup 😀", ct);
        string before = await database.FingerprintAsync(ct);
        ServerBackupArtifact artifact;
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDatabaseMaintenanceProvider<AgentBridgeContextKey> provider = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
            using MaintenanceBudget budget = new(TimeSpan.FromMinutes(2), ct);
            DatabaseInspection inspection = await provider.InspectAsync(budget);
            DatabaseBackupReceipt receipt = await provider.BackupAsync(Guid.NewGuid(), inspection.TargetIdentity, budget);
            artifact = Assert.IsType<ServerBackupArtifact>(receipt.Artifact);
            Assert.True(artifact.IsConfirmed);
        }
        // Изменение source после backup исключает сравнение с текущими данными вместо сохранённого snapshot.
        using (IServiceScope scope = database.Root.CreateScope())
        {
            PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                .BeginAsync(Access(token), token, Guid.NewGuid(), [PersistenceIntegrationTests.Item("{\"text\":\"after backup\"}")], ct));
        }
        await using SqlServerIntegrationDatabase restored = await database.RestoreAsync(artifact, ct);

        Assert.Equal(before, await restored.FingerprintAsync(ct));
        Assert.NotEqual(before, await database.FingerprintAsync(ct));
        string tableNames = string.Join(", ", IntegrationSchemaExpectations.Tables.Select(name => "'" + name + "'"));
        Assert.Equal(IntegrationSchemaExpectations.Tables,
            (await restored.QueryAsync("SELECT name FROM sys.tables WHERE name IN (" + tableNames + ")", ct))
                .Select(row => (string)row["name"]!).Order(StringComparer.Ordinal));
        foreach (string table in LEGACY_DIALOG_TABLES) { Assert.Single(await RowsAsync(restored, table, token, ct)); }
        DialogSnapshot snapshot = await ReadAsync(restored, token, ct);
        Assert.Equal(token.Revision, snapshot.Token.Revision);
        Assert.Equal("backup 😀", Assert.Single(snapshot.Turns).Items[0].Content.GetProperty("text").GetString());
        Assert.Equal("gpt-5", snapshot.Selection!.Model);
        Assert.NotNull(snapshot.ActiveContext);
        using IServiceScope inspect = restored.Root.CreateScope();
        Assert.Empty((await inspect.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
            .InspectAsync(TimeSpan.FromMinutes(2), ct)).PendingMigrations);
    }

    /// <summary>Реальный DDL failure сохраняет SqlException, poison общего gate и восстановимый backup исходной БД.</summary>
    [SqlServerIntegrationFact]
    public async Task ActualMigrationFailurePreservesCausePoisonsGateAndRestoresBackup()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        await database.CreateEmptyAsync(ct);
        await SeedSentinelAsync(database, ct);
        await database.ExecuteAsync("CREATE TABLE [Dialogs] ([Legacy] nvarchar(max) NOT NULL)", ct);
        await database.ExecuteAsync("INSERT INTO [Dialogs] VALUES (N'existing')", ct);
        string before = await database.FingerprintAsync(ct);
        using (IServiceScope scope = database.Root.CreateScope())
        {
            MaintenanceException failure = await Assert.ThrowsAsync<MaintenanceException>(() => scope.ServiceProvider
                .GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().UpdateExistingAsync(TimeSpan.FromMinutes(2), ct));

            Assert.Equal(MaintenanceError.MigrationFailed, failure.Code);
            SqlException cause = Assert.IsType<SqlException>(failure.InnerException);
            Assert.Equal(2714, cause.Number);
            Assert.Contains("Dialogs", cause.Message, StringComparison.Ordinal);
        }
        using (IServiceScope another = database.Root.CreateScope())
        {
            MaintenanceException poisoned = await Assert.ThrowsAsync<MaintenanceException>(() => another.ServiceProvider
                .GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().InspectAsync(TimeSpan.FromSeconds(30), ct));
            Assert.Equal(MaintenanceError.GatePoisoned, poisoned.Code);
        }
        Assert.Equal("existing", Assert.Single(await database.QueryAsync("SELECT * FROM [Dialogs]", ct))["Legacy"]);
        ServerBackupArtifact artifact = await database.ReadBackupAfterFailureAsync(ct);
        await using SqlServerIntegrationDatabase restored = await database.RestoreAsync(artifact, ct);

        Assert.Equal(before, await restored.FingerprintAsync(ct));
        Assert.Equal("сохранено 😀", Assert.Single(await restored.QueryAsync("SELECT * FROM [Sentinel]", ct))["Text"]);
    }

    /// <summary>Создаёт root через публичный production port.</summary>
    private static async Task<DialogWriteToken> CreateAsync(SqlServerIntegrationDatabase database, CancellationToken ct)
    {
        using IServiceScope scope = database.Root.CreateScope();

        return PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogCreator>()
            .CreateAsync(DialogId.From(Guid.NewGuid()), OWNER, NOW, NOW.AddDays(1), ct));
    }

    /// <summary>Наполняет шесть legacy-таблиц настоящими короткими UoW без catalog registration.</summary>
    private static async Task<DialogWriteToken> FillAsync(SqlServerIntegrationDatabase database, DialogWriteToken token, string text, CancellationToken ct)
    {
        using IServiceScope scope = database.Root.CreateScope();
        IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
        Guid turn = Guid.NewGuid();
        token = PersistenceIntegrationTests.Success(await writer.BeginAsync(Access(token), token, turn,
            [PersistenceIntegrationTests.Item(JsonSerializer.Serialize(new { text }))], ct));
        token = PersistenceIntegrationTests.Success(await writer.FinishAsync(Access(token), token, turn, DialogTurnStatus.Completed, [],
            [new(Guid.NewGuid(), PersistenceIntegrationTests.Response(ModelResponseStatus.Completed))], ct));
        token = PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
            .SaveAsync(Access(token), token, 1, PersistenceIntegrationTests.Response(ModelResponseStatus.Completed), ct));
        ModelSettingsSnapshot settings = new(new("gpt-5", true, 200_000, 200_000, 4096, ["high"], "high", ["text"], true, true, true, true, false),
            "high", 32_000, 4096);
        ServiceResult<DialogModelSelection> selection = await scope.ServiceProvider.GetRequiredService<IDialogSettingsWriter>()
            .SaveAsync(Access(token), token, 0, settings, ct);
        Assert.True(selection.Success, selection.Error?.Message);

        return token;
    }

    /// <summary>Читает через production reader в независимом scope.</summary>
    private static async Task<DialogSnapshot> ReadAsync(SqlServerIntegrationDatabase database, DialogWriteToken token, CancellationToken ct)
    {
        using IServiceScope scope = database.Root.CreateScope();
        ServiceResult<DialogSnapshot> result = await scope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(token), ct);
        Assert.True(result.Success, result.Error?.Message);

        return result.Data!;
    }

    /// <summary>Фиксирует явный owner/time для проверяемой записи.</summary>
    private static DialogAccess Access(DialogWriteToken token) => new(token.DialogId, OWNER, NOW);

    /// <summary>Наблюдает конкретные строки root/child таблицы с обязательным parent ID.</summary>
    private static Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RowsAsync(
        SqlServerIntegrationDatabase database, string table, DialogWriteToken token, CancellationToken ct) =>
        database.QueryAsync("SELECT * FROM [" + table + "] WHERE [" + (table is "Dialogs" or "DialogSettings" ? "Id" : "DialogId") + "]=@id",
            ct, new Dictionary<string, object?> { ["@id"] = token.DialogId.Value });

    /// <summary>Создаёт собственные данные до появления schema AgentBridge.</summary>
    private static async Task SeedSentinelAsync(SqlServerIntegrationDatabase database, CancellationToken ct)
    {
        await database.ExecuteAsync("CREATE TABLE [Sentinel] ([Id] int NOT NULL PRIMARY KEY, [Text] nvarchar(max) NOT NULL)", ct);
        await database.ExecuteAsync("INSERT INTO [Sentinel] VALUES (@id,@text)", ct,
            new Dictionary<string, object?> { ["@id"] = 1, ["@text"] = "сохранено 😀" });
    }

    /// <summary>Первая установка и публичные короткие записи сохраняют историю; stale root и expiry запрещают запись.</summary>
    [SqlServerIntegrationFact]
    public async Task InitializeAppendContextRestartAndExpiry()
    {
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        Assert.Equal(MaintenanceOutcome.Initialized, (await database.InitializeNewAsync(TestContext.Current.CancellationToken)).Outcome);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DialogOwnerId owner = DialogOwnerId.From(" User:Б ");
        DialogId id = DialogId.From(Guid.NewGuid());
        DialogAccess access = new(id, owner, now);
        DialogWriteToken token;
        Guid turn = Guid.NewGuid();
        ModelResponse response = PersistenceIntegrationTests.Response(ModelResponseStatus.Completed);
        using (IServiceScope scope = database.Root.CreateScope())
        {
            token = PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogCreator>()
                .CreateAsync(id, owner, now, now.AddHours(1)));
            DialogWriteToken stale = token;
            IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
            token = PersistenceIntegrationTests.Success(await writer.BeginAsync(access, token, turn,
                [PersistenceIntegrationTests.Item("{\"text\":\"Привет 😀\"}")]));
            Assert.Equal(ServiceErrorType.Conflict, (await writer.AppendAsync(access, stale, turn, [], [])).Error!.Type);
            token = PersistenceIntegrationTests.Success(await writer.AppendAsync(access, token, turn, response.Output,
                [new(Guid.NewGuid(), response)]));
            token = PersistenceIntegrationTests.Success(await writer.FinishAsync(access, token, turn, DialogTurnStatus.Completed, [], []));
            token = PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(access, token, 1, response));
        }
        await using ServiceProvider restarted = database.BuildRoot();
        using IServiceScope readScope = restarted.CreateScope();
        IDialogReader reader = readScope.ServiceProvider.GetRequiredService<IDialogReader>();
        ServiceResult<DialogSnapshot> read = await reader.ReadAsync(access);
        Assert.True(read.Success, read.Error?.Message);
        Assert.Equal(token.DialogId, read.Data!.Token.DialogId);
        Assert.Equal(token.IncarnationId, read.Data.Token.IncarnationId);
        Assert.Equal(token.Revision, read.Data.Token.Revision);
        Assert.Equal(now.AddHours(1), read.Data.ExpiresAtUtc);
        Assert.Equal(2, Assert.Single(read.Data.Turns).Items.Count);
        Assert.Single(read.Data.Turns[0].ModelSteps);
        Assert.True(JsonElement.DeepEquals(response.Envelope!.Content, read.Data.Turns[0].ModelSteps[0].Response.Envelope!.Content));
        Assert.True(JsonElement.DeepEquals(response.Continuation!.Content, read.Data.Turns[0].ModelSteps[0].Response.Continuation!.Content));
        Assert.Equal(1, read.Data.ActiveContext!.ThroughTurnSequence);
        Assert.True(JsonElement.DeepEquals(response.Output[0].Content, read.Data.ActiveContext.Compaction.Output[0].Content));
        Assert.Equal(ServiceErrorType.Forbidden, (await reader.ReadAsync(new(id, DialogOwnerId.From(" user:Б "), now))).Error!.Type);
        ServiceResult<DialogWriteToken> expired = await readScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
            .BeginAsync(new(id, owner, now.AddHours(1)), token, Guid.NewGuid(), []);
        Assert.Equal(ServiceErrorType.Expired, expired.Error!.Type);
        Assert.Single((await reader.ReadAsync(access)).Data!.Turns);
        string[] ordinalOwners = ["Owner", "owner", "Owner ", "Owner\0", "Owner\0\0", "Owner\ud800", "Owner\ud801"];
        foreach (string value in ordinalOwners)
        {
            DialogOwnerId exactOwner = DialogOwnerId.From(value);
            DialogId exactId = DialogId.From(Guid.NewGuid());
            DialogAccess exactAccess = new(exactId, exactOwner, now);
            using IServiceScope ownerScope = restarted.CreateScope();
            DialogWriteToken exact = PersistenceIntegrationTests.Success(await ownerScope.ServiceProvider.GetRequiredService<IDialogCreator>()
                .CreateAsync(exactId, exactOwner, now, now.AddHours(1)));
            foreach (string other in ordinalOwners.Where(other => !StringComparer.Ordinal.Equals(value, other)))
            {
                DialogAccess mismatch = new(exactId, DialogOwnerId.From(other), now);
                Assert.Equal(ServiceErrorType.Forbidden, (await ownerScope.ServiceProvider.GetRequiredService<IDialogReader>()
                    .ReadAsync(mismatch)).Error!.Type);
                Assert.Equal(ServiceErrorType.Forbidden, (await ownerScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                    .BeginAsync(mismatch, exact, Guid.NewGuid(), [])).Error!.Type);
            }
            PersistenceIntegrationTests.Success(await ownerScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                .BeginAsync(exactAccess, exact, Guid.NewGuid(), []));
            ServiceResult<DialogSnapshot> roundTrip = await ownerScope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(exactAccess);
            Assert.True(roundTrip.Success, roundTrip.Error?.Message);
            Assert.Equal(value, roundTrip.Data!.OwnerId.Value);
            Assert.Equal(1, roundTrip.Data.Token.Revision);
            Assert.Single(roundTrip.Data.Turns);
        }
    }
}
