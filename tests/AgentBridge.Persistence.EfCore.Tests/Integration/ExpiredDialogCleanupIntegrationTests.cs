using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Public cleanup22 с actual EFCoreLibrary0.0.5, SQLite/PostgreSQL, отдельными scopes и настоящим rollback.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class ExpiredDialogCleanupIntegrationTests
{
    private readonly DatabaseIntegrationFixture environment;

    /// <summary>Получает общее окружение коллекции; каждый случай сохраняет собственную БД.</summary>
    /// <param name="environment">Fixture, владеющий временным каталогом и PostgreSQL-контейнером.</param>
    public ExpiredDialogCleanupIntegrationTests(DatabaseIntegrationFixture environment)
    {
        this.environment = environment;
    }

    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From(" User:Б ");
    private static readonly string[] TABLES = ["Dialogs", "DialogTurns", "ModelSteps", "CanonicalItems", "DialogContexts", "DialogSettings"];

    /// <summary>Равенство expiry включено; один пакет bounded, все шесть таблиц cascade, большой живой диалог сохраняется.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task EqualityBoundedBatchAndCascadeIgnoreSoftBytes(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        List<DialogWriteToken> expired = [];
        foreach (int _ in Enumerable.Range(0, 3)) expired.Add(await FilledAsync(database, NOW.AddHours(2)));
        DialogWriteToken live = await FilledAsync(database, NOW.AddHours(3));
        Probe probe = new() { Clock = { Now = NOW.AddHours(2).AddTicks(-1) } };
        await using ServiceProvider root = BuildRoot(database, probe);
        foreach (DialogWriteToken token in expired.Append(live))
            Assert.True((await PersistenceIntegrationTests.ReadAsync(database, token)).ContentBytes > 1);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        Assert.Empty((await cleanup.CleanupAsync(2)).Candidates);
        Assert.Equal(0, probe.Deletes);
        probe.Clock.Now = NOW.AddHours(2);
        ExpiredDialogCleanupResult batch = await cleanup.CleanupAsync(2);
        Assert.Equal(ExpiredDialogCleanupStatus.Completed, batch.Status);
        Assert.Equal(2, batch.DeletedCount);
        Assert.Equal(2, batch.Candidates.Count);
        Assert.Equal(2, probe.Reads);
        foreach (ExpiredDialogDeletionResult deleted in batch.Candidates) await AssertRowsAsync(database, deleted.Token, 0);
        DialogWriteToken remaining = expired.Single(token => batch.Candidates.All(candidate => !candidate.Token.DialogId.Equals(token.DialogId)));
        await AssertRowsAsync(database, remaining, 1);
        await AssertRowsAsync(database, live, 1);
        Assert.Equal(1, (await cleanup.CleanupAsync(2)).DeletedCount);
        await AssertRowsAsync(database, remaining, 0);
        await AssertRowsAsync(database, live, 1);
        DialogSnapshot survivor = await PersistenceIntegrationTests.ReadAsync(database, live);
        Assert.Equal(NOW.AddHours(3), survivor.ExpiresAtUtc);
        Assert.Equal("gpt-5", survivor.Selection!.Model);
        Assert.Empty((await cleanup.CleanupAsync(2)).Candidates);
        Assert.Equal(3, probe.Deletes);
    }

    /// <summary>Изменение root после snapshot даёт individual Conflict/NotFound; остальные удаляются, refresh/retry нет.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, "revision")]
    [InlineData(DatabaseProvider.PostgreSql, "revision")]
    [InlineData(DatabaseProvider.SQLite, "recreate")]
    [InlineData(DatabaseProvider.PostgreSql, "recreate")]
    [InlineData(DatabaseProvider.SQLite, "removed")]
    [InlineData(DatabaseProvider.PostgreSql, "removed")]
    public async Task StaleCandidateKeepsPartialResultAndDoesNotDeleteNewIncarnation(DatabaseProvider provider, string mutation)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        await FilledAsync(database, NOW.AddHours(1));
        await FilledAsync(database, NOW.AddHours(2));
        await FilledAsync(database, NOW.AddHours(3));
        Probe probe = new() { Clock = { Now = NOW.AddHours(3) } };
        DialogWriteToken? changed = null;
        probe.AfterRead = async candidates =>
        {
            changed = candidates[1];
            using IServiceScope mutationScope = database.Root.CreateScope();
            if (mutation == "revision")
            {
                Success(await mutationScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>().BeginAsync(
                    Access(changed), changed, Guid.NewGuid(), []));
            }
            else
            {
                Assert.True((await mutationScope.ServiceProvider.GetRequiredService<IDialogDeletion>().DeleteAsync(Access(changed), changed)).Success);
                if (mutation == "recreate")
                {
                    DialogWriteToken replacement = await PersistenceIntegrationTests.CreateAsync(database, changed.DialogId, NOW.AddHours(2));
                    Assert.NotEqual(changed.IncarnationId, replacement.IncarnationId);
                }
            }
        };
        await using ServiceProvider root = BuildRoot(database, probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanupResult result = await caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(3);
        Assert.Equal(ExpiredDialogCleanupStatus.Partial, result.Status);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(ExpiredDialogDeletionStatus.Failed, result.Candidates[1].Status);
        Assert.Equal(mutation == "removed" ? ServiceErrorType.NotFound : ServiceErrorType.Conflict, result.Candidates[1].Error!.Type);
        Assert.Equal(3, probe.Deletes);
        Assert.Equal(1, probe.Reads);
        foreach (ExpiredDialogDeletionResult deleted in result.Candidates.Where(candidate => candidate.Status == ExpiredDialogDeletionStatus.Deleted))
            await AssertRowsAsync(database, deleted.Token, 0);
        if (mutation == "revision") await AssertRowsAsync(database, changed!, 1);
        else
        {
            Assert.Equal(mutation == "recreate" ? 1L : 0L, await CountAsync(database, "Dialogs", changed!));
            foreach (string table in TABLES.Skip(1)) Assert.Equal(0L, await CountAsync(database, table, changed!));
        }
    }

    /// <summary>SQL delete выполнен, failure/cancel до commit откатывает второй cascade; первый success не отменяется.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task PartialRealRollbackPreservesRemainingChildren(DatabaseProvider provider, bool cancel)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        List<DialogWriteToken> original = [];
        foreach (int hours in new[] { 1, 2, 3 }) original.Add(await FilledAsync(database, NOW.AddHours(hours)));
        using CancellationTokenSource cancellation = new();
        Probe probe = new() { Clock = { Now = NOW.AddHours(3) }, FailureAt = 2, Cancellation = cancel ? cancellation : null };
        await using ServiceProvider root = BuildRoot(database, probe, services => services.AddScoped<IUnitOfWorkSession, FailureSession>());
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        if (cancel) Assert.Equal(ExpiredDialogCleanupStatus.Canceled, (await cleanup.CleanupAsync(3, cancellation.Token)).Status);
        else await Assert.ThrowsAsync<IOException>(() => cleanup.CleanupAsync(3));
        ExpiredDialogCleanupResult result = cleanup.LastResult!;
        Assert.Equal(cancel ? ExpiredDialogCleanupStatus.Canceled : ExpiredDialogCleanupStatus.Interrupted, result.Status);
        Assert.Equal(new[] { ExpiredDialogDeletionStatus.Deleted, ExpiredDialogDeletionStatus.Unknown, ExpiredDialogDeletionStatus.NotAttempted }, result.Candidates.Select(candidate => candidate.Status));
        Assert.True(probe.SavedEntries > 0);
        Assert.Equal(2, probe.Deletes);
        Assert.Equal(1, probe.Reads);
        await AssertRowsAsync(database, original[0], 0);
        await AssertRowsAsync(database, original[1], 1);
        await AssertRowsAsync(database, original[2], 1);
        await using ServiceProvider restarted = database.BuildRoot();
        using IServiceScope observation = restarted.CreateScope();
        DialogSnapshot survivor = Success(await observation.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(original[1])));
        Assert.Equal("gpt-5", survivor.Selection!.Model);
        Assert.Equal(1, survivor.ActiveContext!.Version);
        Assert.Single(survivor.Turns);
        Assert.Single(survivor.Turns[0].ModelSteps);
    }

    /// <summary>Actual AgentRunner ждёт модель вне transaction; cleanup на equality запрещает late save и revival/recreate.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task CleanupWhileAgentWaitsRejectsLateResponseAndPreservesReplacement(DatabaseProvider provider, bool recreate)
    {
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(provider);
        await database.InitializeAsync();
        DialogWriteToken original = await PersistenceIntegrationTests.CreateAsync(database, expiry: NOW.AddHours(1));
        using (IServiceScope seed = database.Root.CreateScope())
            Success(await seed.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(original), original, 0, Settings()));
        Probe probe = new();
        await using ServiceProvider root = BuildRoot(database, probe, services =>
        {
            services.AddSingleton<IModelGateway, Gateway>();
            services.AddSingleton<IModelSettingsReader, SettingsReader>();
            services.AddSingleton<IModelAccessResolver, Resolver>();
            services.AddAgentBridgeTokenization();
            services.AddScoped<ContextBuilder>(_ => new([]));
            services.AddAgentBridgeTools();
            services.Configure<AgentOptions>(options =>
            {
                options.InstructionsSource = AgentInstructionsSource.Configuration;
                options.Instructions = "cleanup test instructions";
                options.MaxToolSteps = 2;
            });
            services.Configure<ContextCompactionOptions>(options => options.MaxPasses = 2);
            services.AddAgentBridgeRunner();
        });
        ApplicationCallContext call = new(original.DialogId, OWNER, Guid.NewGuid(), "agent");
        await using AsyncServiceScope runnerScope = root.CreateAsyncScope();
        Task<AgentRunResult> running = runnerScope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(
            new(call, [Message("input")], [], new(2, 2, 1, TimeSpan.FromMinutes(1))));
        DialogWriteToken? replacement = null;
        DialogSnapshot? active = null;
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using IServiceScope observe = root.CreateScope();
            active = Success(await observe.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(original)));
            Assert.Equal(DialogTurnStatus.InProgress, active.Turns[0].Status);
            Assert.Equal("gpt-5", active.Turns[0].Settings!.Model);
            probe.Clock.Now = active.ExpiresAtUtc!.Value;
            using IServiceScope caller = root.CreateScope();
            Assert.Equal(1, (await caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(1)).DeletedCount);
            await AssertRowsAsync(database, original, 0);
            if (recreate)
            {
                using IServiceScope create = root.CreateScope();
                replacement = Success(await create.ServiceProvider.GetRequiredService<IDialogCreator>().CreateAsync(
                    original.DialogId, OWNER, probe.Clock.Now, probe.Clock.Now.AddHours(1)));
            }
        }
        finally { probe.Release.TrySetResult(); await running; }
        AgentRunResult result = await running;
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(recreate ? ServiceErrorType.Conflict : ServiceErrorType.NotFound, result.Error!.Type);
        Assert.False(result.TerminalSaved);
        Assert.NotNull(result.LastResponse);
        Assert.Equal(1, probe.Generations);
        using (IServiceScope late = root.CreateScope())
        {
            DialogAccess access = new(original.DialogId, OWNER, probe.Clock.Now);
            Assert.Equal(result.Error.Type, (await late.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(access, active!.Token, 0, ModelResponse.Completed([Message("late compact")]))).Error!.Type);
            Assert.Equal(result.Error.Type, (await late.ServiceProvider.GetRequiredService<IDialogSettingsWriter>()
                .SaveAsync(access, active.Token, 1, Settings())).Error!.Type);
        }
        Assert.Equal(recreate ? 1L : 0L, await CountAsync(database, "Dialogs", original));
        foreach (string table in TABLES.Skip(1)) Assert.Equal(0L, await CountAsync(database, table, original));
        if (replacement is not null)
        {
            DialogSnapshot current = await PersistenceIntegrationTests.ReadAsync(database, replacement);
            Assert.Equal(replacement.IncarnationId, current.Token.IncarnationId);
            Assert.Equal(0, current.Token.Revision);
            Assert.Empty(current.Turns);
        }
    }

    /// <summary>Подключает public cleanup и тонкие наблюдатели actual persistence портов.</summary>
    private static ServiceProvider BuildRoot(IntegrationDatabase database, Probe probe, Action<ServiceCollection>? customize = null) =>
        database.BuildRoot(customize: services =>
        {
            services.AddSingleton(probe);
            services.AddSingleton<TimeProvider>(probe.Clock);
            services.Configure<DialogRetentionOptions>(options => { options.SoftContentLimitBytes = 1; options.RetentionPeriod = TimeSpan.FromHours(36); });
            services.AddScoped<ExpiredDialogReader>();
            services.AddScoped<IExpiredDialogReader, ObservedReader>();
            services.AddScoped<IExpiredDialogDeletion, ObservedDeletion>();
            services.AddAgentBridgeDialogCleanup();
            customize?.Invoke(services);
        });

    /// <summary>Наполняет все шесть таблиц через public порты до истечения.</summary>
    private static async Task<DialogWriteToken> FilledAsync(IntegrationDatabase database, DateTimeOffset expiry)
    {
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database, expiry: expiry);
        token = await PersistenceIntegrationTests.FillAsync(database, token, Guid.NewGuid(), Guid.NewGuid(), new string('x', 1024));
        using IServiceScope scope = database.Root.CreateScope();
        Success(await scope.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, 0, Settings()));
        return token;
    }

    /// <summary>Наблюдает наличие либо отсутствие всех зависимых наборов конкретного диалога.</summary>
    private static async Task AssertRowsAsync(IntegrationDatabase database, DialogWriteToken token, int minimum)
    {
        foreach (string table in TABLES)
        {
            long count = await CountAsync(database, table, token);
            if (minimum == 0) Assert.Equal(0L, count); else Assert.True(count >= minimum, table);
        }
    }

    /// <summary>Читает count через разрешённые commands EFCoreLibrary, не подменяя production операции.</summary>
    private static async Task<long> CountAsync(IntegrationDatabase database, string table, DialogWriteToken token)
    {
        // Имена из фиксированного списка; GUID создаётся тестом. Только наблюдение через actual библиотечные commands.
        string key = table is "Dialogs" or "DialogSettings" ? "Id" : "DialogId";
        string id = token.DialogId.Value.ToString("D");
        if (database.Provider == DatabaseProvider.SQLite) id = id.ToUpperInvariant();
        return Convert.ToInt64(Assert.Single(await database.QueryAsync($"SELECT count(*) AS count FROM \"{table}\" WHERE \"{key}\" = '{id}'"))["count"]);
    }

    /// <summary>Исторический доступ до expiry для подготовки данных и управляемого interleaving.</summary>
    private static DialogAccess Access(DialogWriteToken token) => new(token.DialogId, OWNER, NOW);
    /// <summary>Изолированные capabilities выбранной модели с бюджетом выше actual offline input.</summary>
    private static ModelSettingsSnapshot Settings() => new(new("gpt-5", true, 100000, 100000, 100, ["high"], "high", ["text"], true, true, true, true, false), "high", 90000, 10);
    /// <summary>Проверяет подтверждение порта до использования данных.</summary>
    private static T Success<T>(ServiceResult<T> result) where T : class { Assert.True(result.Success, result.Error?.Message); return result.Data!; }
    /// <summary>Создаёт synthetic canonical input/output без HTTP.</summary>
    private static CanonicalModelItem Message(string text) => new(JsonSerializer.SerializeToElement(new { type = "message", role = "user", content = text }));

    /// <summary>Синхронизирует контролируемые DB interleavings и модельное ожидание.</summary>
    private class Probe
    {
        public Clock Clock { get; } = new();
        public int Reads, Deletes, FailureAt, SavedEntries, Generations;
        public CancellationTokenSource? Cancellation;
        public Func<IReadOnlyList<DialogWriteToken>, Task>? AfterRead;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Фиксирует явное UTC-время сценария без задержек wall clock.</summary>
    private class Clock : TimeProvider
    {
        public DateTimeOffset Now = NOW;
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <inheritdoc/>
    private class ObservedReader(ExpiredDialogReader inner, Probe probe) : IExpiredDialogReader
    {
        /// <inheritdoc/>
        public async Task<ServiceResult<IReadOnlyList<DialogWriteToken>>> ReadAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken = default)
        {
            probe.Reads++;
            ServiceResult<IReadOnlyList<DialogWriteToken>> result = await inner.ReadAsync(nowUtc, limit, cancellationToken);
            if (result.Success && probe.AfterRead is not null) await probe.AfterRead(result.Data!);
            return result;
        }
    }

    /// <inheritdoc/>
    private class ObservedDeletion(DialogDeletionUnitOfWork inner, Probe probe) : IExpiredDialogDeletion
    {
        /// <inheritdoc/>
        public Task<ServiceResult> DeleteAsync(DialogWriteToken expected, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        { probe.Deletes++; return inner.DeleteAsync(expected, nowUtc, cancellationToken); }
    }

    /// <inheritdoc/>
    private class FailureSession(IUnitOfWorkContext<AgentBridgeContextKey> context, Probe probe) : IUnitOfWorkSession
    {
        private readonly EfUnitOfWorkSession inner = new(context);
        /// <inheritdoc/>
        public Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken) => inner.BeginAsync(cancellationToken);
        /// <inheritdoc/>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            int saved = await inner.SaveChangesAsync(cancellationToken);
            if (probe.Deletes == probe.FailureAt)
            {
                probe.SavedEntries = saved;
                if (probe.Cancellation is not null) { probe.Cancellation.Cancel(); throw new OperationCanceledException(probe.Cancellation.Token); }
                throw new IOException("Тестовый отказ после настоящего cascade SQL до commit.");
            }
            return saved;
        }
        /// <inheritdoc/>
        public void Clear() => inner.Clear();
    }

    /// <inheritdoc/>
    private class Gateway(Probe probe) : IModelGateway
    {
        /// <inheritdoc/>
        public async Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        { probe.Generations++; probe.Entered.TrySetResult(); await probe.Release.Task.WaitAsync(cancellationToken); return ServiceResult<ModelResponse>.Ok(ModelResponse.Completed([Message("late model")])); }
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Actual offline budget ниже threshold; compact не ожидается.");
    }

    /// <inheritdoc/>
    private class SettingsReader : IModelSettingsReader
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Run использует pinned access.");
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access, string? model = null, string? effort = null, CancellationToken cancellationToken = default)
        { Assert.Equal("gpt-5", model); return Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Ok(Settings())); }
    }

    /// <inheritdoc/>
    private class Resolver : IModelAccessResolver
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ModelAccess>.Ok(new("test-only-access")));
    }
}
