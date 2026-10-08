using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Реальные persistence границы этапов 06–11 на SQLite и PostgreSQL, без приложения и HTTP.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class PersistenceIntegrationTests
{
    private readonly DatabaseIntegrationFixture environment;

    /// <summary>Получает общее окружение коллекции; каждый случай сохраняет собственную БД.</summary>
    /// <param name="environment">Fixture, владеющий временным каталогом и PostgreSQL-контейнером.</param>
    public PersistenceIntegrationTests(DatabaseIntegrationFixture environment)
    {
        this.environment = environment;
    }

    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From(" User:Б ");

    /// <summary>После нового root-container сохраняются все lifecycle, opaque payload, версии и fixed expiry.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, ModelResponseStatus.Completed)]
    [InlineData(DatabaseProvider.SQLite, ModelResponseStatus.Incomplete)]
    [InlineData(DatabaseProvider.SQLite, ModelResponseStatus.Failed)]
    [InlineData(DatabaseProvider.SQLite, ModelResponseStatus.Canceled)]
    [InlineData(DatabaseProvider.PostgreSql, ModelResponseStatus.Completed)]
    [InlineData(DatabaseProvider.PostgreSql, ModelResponseStatus.Incomplete)]
    [InlineData(DatabaseProvider.PostgreSql, ModelResponseStatus.Failed)]
    [InlineData(DatabaseProvider.PostgreSql, ModelResponseStatus.Canceled)]
    public async Task RestartRoundTripPreservesFullHistory(DatabaseProvider provider, ModelResponseStatus status)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        Guid turn = Guid.NewGuid();
        ModelResponse response = Response(status);
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
            token = Success(await writer.BeginAsync(Access(token), token, turn, [Item("{\"text\":\"Привет 😀\"}")]));
            Assert.Equal(ServiceErrorType.Conflict, (await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(Access(token), token, 1, Response(ModelResponseStatus.Completed))).Error!.Type);
            token = Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(Access(token), token, 0, Response(ModelResponseStatus.Completed)));
            token = Success(await writer.AppendAsync(Access(token), token, turn, response.Output, [new(Guid.NewGuid(), response)]));
            token = Success(await writer.FinishAsync(Access(token), token, turn, DialogTurnStatus.Completed, [], []));
            token = Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(Access(token), token, 1, Response(ModelResponseStatus.Completed)));
            token = Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(Access(token), token, 1, Response(ModelResponseStatus.Completed)));
            Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        }
        using ServiceProvider restarted = database.BuildRoot();
        using IServiceScope restartedScope = restarted.CreateScope();
        DialogSnapshot snapshot = (await restartedScope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(token))).Data!;
        Assert.Equal(token.Revision, snapshot.Token.Revision);
        Assert.Equal(token.IncarnationId, snapshot.Token.IncarnationId);
        Assert.Equal(NOW, snapshot.CreatedAtUtc);
        Assert.Equal(NOW.AddHours(36), snapshot.ExpiresAtUtc);
        Assert.Equal(OWNER, snapshot.OwnerId);
        StoredDialogTurn storedTurn = Assert.Single(snapshot.Turns);
        Assert.Equal(2, storedTurn.Items.Count);
        ModelResponse stored = Assert.Single(storedTurn.ModelSteps).Response;
        Assert.Equal(status, stored.Status);
        Assert.True(JsonElement.DeepEquals(response.Output[0].Content, stored.Output[0].Content));
        Assert.True(JsonElement.DeepEquals(response.Envelope!.Content, stored.Envelope!.Content));
        Assert.True(JsonElement.DeepEquals(response.Continuation!.Content, stored.Continuation!.Content));
        Assert.Equal(response.Error?.Message, stored.Error?.Message);
        Assert.Equal(3, snapshot.ActiveContext!.Version);
        Assert.Equal(1, snapshot.ActiveContext.ThroughTurnSequence);
        IReadOnlyList<IReadOnlyDictionary<string, object?>> contexts = await database.QueryAsync("SELECT \"Version\", \"ThroughTurnSequence\" FROM \"DialogContexts\" ORDER BY \"Version\"");
        Assert.Equal(new[] { 0L, 1L, 1L }, contexts.Select(row => Convert.ToInt64(row["ThroughTurnSequence"])));
        long expectedBytes = 0;
        foreach (string table in new[] { "CanonicalItems", "ModelSteps", "DialogContexts" })
        {
            string columns = table == "CanonicalItems" ? "\"ContentJson\"" :
                "\"ResponseOutputJson\", \"ResponseEnvelopeJson\", \"ResponseContinuationJson\", \"ResponseErrorMessage\"";
            foreach (IReadOnlyDictionary<string, object?> row in await database.QueryAsync("SELECT " + columns + " FROM \"" + table + "\""))
            {
                expectedBytes += row.Values.OfType<string>().Sum(value => (long)Encoding.UTF8.GetByteCount(value));
            }
        }
        Assert.Equal(expectedBytes, snapshot.ContentBytes);
        Assert.True(snapshot.IsExpired(NOW.AddHours(36)));
        ServiceResult<DialogWriteToken> continued = await restartedScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
            .BeginAsync(Access(token), token, Guid.NewGuid(), []);
        Assert.True(continued.Success, continued.Error?.Message);
    }

    /// <summary>Guards на сохранённом root не принимают чужого владельца, истёкший срок и старую жизнь ID.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task GuardsAndDeleteRecreateRejectLateWrites(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        using IServiceScope scope = database.Root.CreateScope();
        IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
        Guid turn = Guid.NewGuid();
        Assert.Equal(ServiceErrorType.Forbidden, (await scope.ServiceProvider.GetRequiredService<IDialogReader>()
            .ReadAsync(new(token.DialogId, DialogOwnerId.From("User:Б"), NOW))).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await scope.ServiceProvider.GetRequiredService<IDialogCreator>()
            .CreateAsync(token.DialogId, DialogOwnerId.From("different"), NOW, NOW.AddDays(7))).Error!.Type);
        Assert.Equal(ServiceErrorType.Forbidden, (await writer.BeginAsync(new(token.DialogId, DialogOwnerId.From("User:Б"), NOW), token, turn, [])).Error!.Type);
        Assert.Equal(ServiceErrorType.Expired, (await writer.BeginAsync(Access(token, NOW.AddHours(36)), token, turn, [])).Error!.Type);
        DialogWriteToken fresh = Success(await writer.BeginAsync(Access(token), token, turn, [Item("{\"text\":\"one\"}")]));
        Assert.Equal(ServiceErrorType.Conflict, (await writer.AppendAsync(Access(token), token, turn, [], [])).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await scope.ServiceProvider.GetRequiredService<IExpiredDialogDeletion>().DeleteAsync(token, NOW.AddHours(36))).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await scope.ServiceProvider.GetRequiredService<IExpiredDialogDeletion>().DeleteAsync(fresh, NOW.AddHours(36).AddTicks(-1))).Error!.Type);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IDialogDeletion>().DeleteAsync(Access(fresh, NOW.AddHours(36)), fresh)).Success);
        Assert.Equal(ServiceErrorType.NotFound, (await writer.AppendAsync(Access(fresh), fresh, turn, [], [])).Error!.Type);
        DialogWriteToken recreated = await CreateAsync(database, token.DialogId);
        Assert.NotEqual(token.IncarnationId, recreated.IncarnationId);
        Assert.Equal(ServiceErrorType.Conflict, (await writer.BeginAsync(Access(fresh), fresh, Guid.NewGuid(), [])).Error!.Type);
        Assert.Empty((await ReadAsync(database, recreated)).Turns);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        Assert.True((await scope.ServiceProvider.GetRequiredService<IExpiredDialogDeletion>().DeleteAsync(recreated, NOW.AddHours(36))).Success);
    }

    /// <summary>Одинаковые локальные IDs не смешивают детей; каскад удаляет все четыре вида детей только своего root.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task LocalIdsAndCascadeAreIsolated(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken first = await CreateAsync(database);
        DialogWriteToken second = await CreateAsync(database);
        Guid turnId = Guid.NewGuid();
        Guid stepId = Guid.NewGuid();
        first = await FillAsync(database, first, turnId, stepId, "first");
        second = await FillAsync(database, second, turnId, stepId, "second");
        Assert.Equal("first", (await ReadAsync(database, first)).Turns[0].Items[0].Content.GetProperty("text").GetString());
        Assert.Equal("second", (await ReadAsync(database, second)).Turns[0].Items[0].Content.GetProperty("text").GetString());
        using IServiceScope scope = database.Root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IDialogDeletion>().DeleteAsync(Access(first), first)).Success);
        foreach (string table in new[] { "Dialogs", "DialogTurns", "CanonicalItems", "ModelSteps", "DialogContexts" })
        {
            Assert.Equal(1L, Convert.ToInt64(Assert.Single(await database.QueryAsync("SELECT count(*) AS count FROM \"" + table + "\""))["count"]));
        }
        Assert.Single((await ReadAsync(database, second)).Turns);
    }

    /// <summary>Срок сравнивается с точностью ticks, сортировка/limit происходят в настоящем provider.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ExpiredCandidatesUseTicksAndLimitAfterSorting(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DateTimeOffset boundary = NOW.AddHours(36);
        DialogWriteToken future = await CreateAsync(database, expiry: boundary.AddTicks(1));
        DialogWriteToken exact = await CreateAsync(database, expiry: boundary);
        DialogWriteToken earlier = await CreateAsync(database, expiry: boundary.AddTicks(-1));
        using IServiceScope scope = database.Root.CreateScope();
        IExpiredDialogReader reader = scope.ServiceProvider.GetRequiredService<IExpiredDialogReader>();
        IReadOnlyList<DialogWriteToken> limited = (await reader.ReadAsync(boundary, 1)).Data!;
        Assert.Equal(earlier.DialogId, Assert.Single(limited).DialogId);
        Assert.Equal(new[] { earlier.DialogId, exact.DialogId }, (await reader.ReadAsync(boundary, 10)).Data!.Select(token => token.DialogId));
        Assert.DoesNotContain((await reader.ReadAsync(boundary, 10)).Data!, token => token.DialogId.Equals(future.DialogId));
    }

    /// <summary>Реальный optimistic UPDATE со старым original revision откатывает подготовленных детей.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ActualCasFailureRollsBackChildWrites(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        using IServiceScope staleScope = database.Root.CreateScope();
        DialogRecord stale = (await staleScope.ServiceProvider.GetRequiredService<DialogRecordQueries>().FindAsync(token.DialogId.Value, trackChanges: true))!;
        using (IServiceScope winner = database.Root.CreateScope())
        {
            Assert.True((await winner.ServiceProvider.GetRequiredService<IDialogTurnWriter>().BeginAsync(Access(token), token, Guid.NewGuid(), [])).Success);
        }
        ServiceResult result = await staleScope.ServiceProvider.GetRequiredService<UnitOfWorkScope>().ExecuteAsync(_ =>
        {
            stale.Revision++;
            staleScope.ServiceProvider.GetRequiredService<RecordStaging<DialogRecord>>().StageUpdate(stale);
            staleScope.ServiceProvider.GetRequiredService<RecordStaging<DialogTurnRecord>>().StageCreate(new()
            {
                DialogId = stale.Id, Id = Guid.NewGuid(), Sequence = 2, StartedAtUtc = NOW
            });
            return Task.FromResult(ServiceResult.Ok());
        }, default);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Single((await ReadAsync(database, token)).Turns);
        Assert.Empty(staleScope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
    }

    /// <summary>Настоящий root-PK collision распознаётся как Conflict, fixed поля не допускают изменение после загрузки.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task RootCollisionAndFixedFieldsAreEnforced(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        using IServiceScope scope = database.Root.CreateScope();
        ServiceResult<DialogWriteToken> collision = await scope.ServiceProvider.GetRequiredService<UnitOfWorkScope>().ExecuteAsync(_ =>
        {
            scope.ServiceProvider.GetRequiredService<RecordStaging<DialogRecord>>().StageCreate(RootRecord(identity: token.DialogId.Value));
            return Task.FromResult(ServiceResult<DialogWriteToken>.Ok(token));
        }, default, creatingDialog: true);
        Assert.Equal(ServiceErrorType.Conflict, collision.Error!.Type);
        string before = await database.FingerprintAsync();
        foreach (string field in new[] { "owner", "incarnation", "created", "expiry" })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<UnitOfWorkScope>().ExecuteAsync(async ct =>
            {
                DialogRecord root = (await scope.ServiceProvider.GetRequiredService<DialogRecordQueries>().FindAsync(token.DialogId.Value, ct, true))!;
                switch (field)
                {
                    case "owner": root.OwnerId = "changed"; break;
                    case "incarnation": root.IncarnationId = Guid.NewGuid(); break;
                    case "created": root.CreatedAtUtc = NOW.AddTicks(-1); break;
                    case "expiry": root.ExpiresAtUtc = NOW.AddDays(100); break;
                }
                scope.ServiceProvider.GetRequiredService<RecordStaging<DialogRecord>>().StageUpdate(root);
                return ServiceResult.Ok();
            }, default));
            Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
            Assert.Equal(before, await database.FingerprintAsync());
        }
    }

    /// <summary>Ошибка после действительного SQL-save откатывает root и детей, а scope очищает tracker.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task FailureAfterRealSaveRollsBackWholeScenario(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        string before = await database.FingerprintAsync();
        using ServiceProvider failureRoot = database.BuildRoot(customize: services => services.AddScoped<IUnitOfWorkSession, ThrowAfterSaveSession>());
        using IServiceScope scope = failureRoot.CreateScope();
        await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
            .BeginAsync(Access(token), token, Guid.NewGuid(), [Item("{\"text\":\"rollback\"}")]));
        Assert.True(((ThrowAfterSaveSession)scope.ServiceProvider.GetRequiredService<IUnitOfWorkSession>()).SavedEntries >= 3);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        Assert.Equal(before, await database.FingerprintAsync());
    }

    /// <summary>Два независимых Serializable сценария одного token сохраняют только одного победителя без retry.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task SimultaneousWritersCannotBothCommit(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        ConcurrentReadBarrier barrier = new();
        using ServiceProvider raceRoot = database.BuildRoot(customize: services =>
        {
            if (provider == DatabaseProvider.PostgreSql)
            {
                services.AddDbContext<AgentBridgeDbContext>(options => options.AddInterceptors(barrier));
            }
        });
        using IServiceScope first = raceRoot.CreateScope();
        using IServiceScope second = raceRoot.CreateScope();
        Task<(ServiceResult<DialogWriteToken>? Result, Exception? Error)> left = Task.Run(() => AttemptAsync(first));
        Task<(ServiceResult<DialogWriteToken>? Result, Exception? Error)> right = Task.Run(() => AttemptAsync(second));
        (ServiceResult<DialogWriteToken>? Result, Exception? Error)[] outcomes = await Task.WhenAll(left, right);
        Assert.Single(outcomes, item => item.Result?.Success == true);
        (ServiceResult<DialogWriteToken>? Result, Exception? Error) loser = Assert.Single(outcomes, item => item.Result?.Success != true);
        if (loser.Error is not null)
        {
            Exception driver = loser.Error;
            while (driver.InnerException is not null) { driver = driver.InnerException; }
            Assert.True(provider == DatabaseProvider.SQLite
                ? driver is SqliteException { SqliteErrorCode: 5 or 6 }
                : driver is PostgresException { SqlState: "40001" }, loser.Error.ToString());
        }
        else { Assert.Equal(ServiceErrorType.Conflict, loser.Result!.Error!.Type); }
        DialogSnapshot final = await ReadAsync(database, token);
        Assert.Equal(1, final.Token.Revision);
        Assert.Single(final.Turns);
        Assert.Single(final.Turns[0].Items);
        Assert.Empty(first.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        Assert.Empty(second.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());

        async Task<(ServiceResult<DialogWriteToken>?, Exception?)> AttemptAsync(IServiceScope scope)
        {
            try { return (await scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                .BeginAsync(Access(token), token, Guid.NewGuid(), [Item("{\"text\":\"race\"}")]), null); }
            catch (Exception error) { return (null, error); }
        }
    }

    /// <summary>Реальные FK, unique и check constraints отвергают неправильные записи без изменения сохранённых данных.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task RelationalConstraintsRejectInvalidRows(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await CreateAsync(database);
        Guid turn = Guid.NewGuid();
        token = await FillAsync(database, token, turn, Guid.NewGuid(), "valid");
        string before = await database.FingerprintAsync();
        List<object> invalid =
        [
            RootRecord(revision: -1), RootRecord(bytes: -1), RootRecord(owner: ""), RootRecord(identity: Guid.Empty),
            RootRecord(expiry: NOW), RootRecord(changed: NOW.AddTicks(-1)),
            new DialogTurnRecord { DialogId = Guid.NewGuid(), Id = Guid.NewGuid(), Sequence = 1, StartedAtUtc = NOW },
            new DialogTurnRecord { DialogId = token.DialogId.Value, Id = Guid.NewGuid(), Sequence = 1, StartedAtUtc = NOW },
            new DialogTurnRecord { DialogId = token.DialogId.Value, Id = Guid.NewGuid(), Sequence = 0, StartedAtUtc = NOW },
            new DialogTurnRecord { DialogId = token.DialogId.Value, Id = Guid.NewGuid(), Sequence = 2, StartedAtUtc = NOW, Status = DialogTurnStatus.Completed },
            new DialogTurnRecord { DialogId = token.DialogId.Value, Id = Guid.NewGuid(), Sequence = 2, StartedAtUtc = NOW, Status = (DialogTurnStatus)5 },
            new CanonicalItemRecord { DialogId = token.DialogId.Value, TurnId = Guid.NewGuid(), Sequence = 1, ContentJson = "{}" },
            new CanonicalItemRecord { DialogId = token.DialogId.Value, TurnId = turn, Sequence = 0, ContentJson = "{}" },
            new ModelStepRecord { DialogId = token.DialogId.Value, TurnId = turn, Id = Guid.NewGuid(), Sequence = 1 },
            new ModelStepRecord { DialogId = token.DialogId.Value, TurnId = turn, Id = Guid.NewGuid(), Sequence = 2, Response = new() { FormatVersion = 2 } },
            new ModelStepRecord { DialogId = token.DialogId.Value, TurnId = turn, Id = Guid.NewGuid(), Sequence = 2, Response = new() { Status = ModelResponseStatus.Failed } },
            new ModelStepRecord { DialogId = token.DialogId.Value, TurnId = turn, Id = Guid.NewGuid(), Sequence = 2, Response = new() { Status = (ModelResponseStatus)4 } },
            new ModelStepRecord { DialogId = token.DialogId.Value, TurnId = turn, Id = Guid.NewGuid(), Sequence = 2, Response = new() { OutputJson = null! } },
            new DialogContextRecord { DialogId = token.DialogId.Value, Version = 2, CreatedAtUtc = NOW, ThroughTurnSequence = -1 },
            new DialogContextRecord { DialogId = token.DialogId.Value, Version = 2, CreatedAtUtc = NOW, Compaction = new() { Status = ModelResponseStatus.Incomplete } }
        ];
        foreach (object row in invalid)
        {
            using IServiceScope scope = database.Root.CreateScope();
            DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<UnitOfWorkScope>().ExecuteAsync(_ =>
            {
                switch (row)
                {
                    case DialogRecord root: scope.ServiceProvider.GetRequiredService<RecordStaging<DialogRecord>>().StageCreate(root); break;
                    case DialogTurnRecord child: scope.ServiceProvider.GetRequiredService<RecordStaging<DialogTurnRecord>>().StageCreate(child); break;
                    case CanonicalItemRecord item: scope.ServiceProvider.GetRequiredService<RecordStaging<CanonicalItemRecord>>().StageCreate(item); break;
                    case ModelStepRecord step: scope.ServiceProvider.GetRequiredService<RecordStaging<ModelStepRecord>>().StageCreate(step); break;
                    case DialogContextRecord context: scope.ServiceProvider.GetRequiredService<RecordStaging<DialogContextRecord>>().StageCreate(context); break;
                }
                return Task.FromResult(ServiceResult.Ok());
            }, default));
            Assert.True(provider == DatabaseProvider.SQLite
                ? error.InnerException is SqliteException { SqliteErrorCode: 19 }
                : error.InnerException is PostgresException { SqlState: "23502" or "23503" or "23505" or "23514" });
            Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
            Assert.Equal(before, await database.FingerprintAsync());
        }
    }

    /// <summary>Создаёт root через настоящий публичный write port.</summary>
    internal static async Task<DialogWriteToken> CreateAsync(IntegrationDatabase database, DialogId? id = null, DateTimeOffset? expiry = null)
    {
        using IServiceScope scope = database.Root.CreateScope();
        return Success(await scope.ServiceProvider.GetRequiredService<IDialogCreator>().CreateAsync(id ?? DialogId.From(Guid.NewGuid()), OWNER,
            NOW, expiry ?? NOW.AddHours(36)));
    }

    /// <summary>Читает через публичный read port; token здесь служит только идентификатором.</summary>
    internal static async Task<DialogSnapshot> ReadAsync(IntegrationDatabase database, DialogWriteToken token)
    {
        using IServiceScope scope = database.Root.CreateScope();
        ServiceResult<DialogSnapshot> result = await scope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(token));
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!;
    }

    /// <summary>Наполняет все таблицы через существующие короткие порты.</summary>
    internal static async Task<DialogWriteToken> FillAsync(IntegrationDatabase database, DialogWriteToken token, Guid turn, Guid step, string text)
    {
        using IServiceScope scope = database.Root.CreateScope();
        IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
        token = Success(await writer.BeginAsync(Access(token), token, turn, [Item(JsonSerializer.Serialize(new { text }))]));
        token = Success(await writer.FinishAsync(Access(token), token, turn, DialogTurnStatus.Completed, [], [new(step, Response(ModelResponseStatus.Completed))]));
        return Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>().SaveAsync(Access(token), token, 1, Response(ModelResponseStatus.Completed)));
    }

    /// <summary>Фиксирует явное UTC-время и ordinal-владельца.</summary>
    internal static DialogAccess Access(DialogWriteToken token, DateTimeOffset? now = null) => new(token.DialogId, OWNER, now ?? NOW);
    /// <summary>Проверяет факт сохранённого успеха порта, не маскируя отказ.</summary>
    internal static DialogWriteToken Success(ServiceResult<DialogWriteToken> result)
    {
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!;
    }
    /// <summary>Независимый JSON-снимок с неизвестными полями.</summary>
    internal static CanonicalModelItem Item(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(document.RootElement);
    }
    /// <summary>Полный lifecycle report без сети, аккаунтов или токенов.</summary>
    internal static ModelResponse Response(ModelResponseStatus status)
    {
        CanonicalModelItem item = Item("{\"type\":\"function_call_output\",\"call_id\":\"c1\",\"output\":\"результат 😀\",\"unknown\":42}");
        using JsonDocument envelope = JsonDocument.Parse("{\"id\":\"r1\",\"opaque\":\"abc\",\"unknown\":[1,2]}");
        using JsonDocument continuation = JsonDocument.Parse("{\"owner\":\"synthetic-upstream\",\"cursor\":\"next\"}");
        CanonicalModelEnvelope full = new(envelope.RootElement);
        ModelContinuation next = new(continuation.RootElement);
        return status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed([item], full, next),
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete([item], full, next),
            ModelResponseStatus.Canceled => ModelResponse.Canceled([item], full, next),
            ModelResponseStatus.Failed => ModelResponse.Failed([item], new(ServiceErrorType.Rejected, "Отказ."), full, next),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
    /// <summary>Готовит DTO для проверки enforcement схемы, не ослабляя Domain.</summary>
    private static DialogRecord RootRecord(long revision = 0, long bytes = 0, string owner = "valid", Guid? identity = null,
        DateTimeOffset? expiry = null, DateTimeOffset? changed = null) => new()
    {
        Id = identity ?? Guid.NewGuid(), IncarnationId = Guid.NewGuid(), Revision = revision, ContentBytes = bytes, OwnerId = owner,
        CreatedAtUtc = NOW, ExpiresAtUtc = expiry ?? NOW.AddDays(1), LastChangedAtUtc = changed ?? NOW
    };
}
