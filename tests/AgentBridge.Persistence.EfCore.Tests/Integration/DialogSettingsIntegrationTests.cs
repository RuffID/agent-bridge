using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Actual settings21 persistence, независимый active run, rollback и historical Up/Down обоих providers.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class DialogSettingsIntegrationTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From(" User:Б ");

    /// <summary>Выбор survives новый root, относится только к диалогу и не меняет token/expiry; stale CAS отклоняется.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task IndependentSelectionSurvivesRestartAndRejectsStaleVersion(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        DialogWriteToken other = await PersistenceIntegrationTests.CreateAsync(database);
        using (IServiceScope first = database.Root.CreateScope())
            Assert.Equal(1, Success(await first.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, 0, Settings("gpt-5", "high"))).Version);
        await using ServiceProvider restarted = database.BuildRoot();
        DialogSnapshot saved = await ReadAsync(restarted, token);
        Assert.Equal("gpt-5", saved.Selection!.Model);
        Assert.Equal("high", saved.Selection.Effort);
        Assert.Equal(token.Revision, saved.Token.Revision);
        Assert.Equal(NOW.AddHours(36), saved.ExpiresAtUtc);
        Assert.Equal(0, saved.ContentBytes);
        Assert.Null((await ReadAsync(restarted, other)).Selection);
        using IServiceScope second = restarted.CreateScope();
        IDialogSettingsWriter writer = second.ServiceProvider.GetRequiredService<IDialogSettingsWriter>();
        Assert.Equal(ServiceErrorType.Conflict, (await writer.SaveAsync(Access(token), saved.Token, 0, Settings("gpt-4", "low"))).Error!.Type);
        Assert.Equal(2, Success(await writer.SaveAsync(Access(token), saved.Token, 1, Settings("gpt-4", "low"))).Version);
        Assert.Equal(0, (await ReadAsync(restarted, token)).Token.Revision);
    }

    /// <summary>Смена выбора в другом scope не инвалидирует активный ход; следующий run и override используют новый snapshot.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ActiveRunKeepsSnapshotAndNextRunUsesStoredSelectionWithEffortOverride(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        Probe probe = new();
        await using ServiceProvider root = BuildRoot(database, probe);
        using (IServiceScope initial = root.CreateScope())
            Success(await initial.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, 0, Settings("gpt-5", "high")));
        ApplicationCallContext call = new(token.DialogId, OWNER, Guid.NewGuid(), "agent");
        Task<AgentRunResult> running = RunAsync(root, call);
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            DialogSnapshot inProgress = await ReadAsync(root, token);
            Assert.Equal("gpt-5", inProgress.Turns[0].Settings!.Model);
            Assert.Equal("high", inProgress.Turns[0].Settings!.Effort);
            using IServiceScope settings = root.CreateScope();
            Success(await settings.ServiceProvider.GetRequiredService<AgentSettingsService>()
                .SelectAsync(call, inProgress.Token, 1, "gpt-4", "low"));
            Assert.Equal(inProgress.Token.Revision, (await ReadAsync(root, token)).Token.Revision);
        }
        finally { probe.Release.TrySetResult(); await running; }
        AgentRunResult completed = await running;
        Assert.Equal(AgentRunStatus.Completed, completed.Status);
        Assert.True(completed.TerminalSaved);
        Assert.Equal("gpt-5", completed.Settings!.Model.Id);
        ApplicationCallContext next = new(token.DialogId, OWNER, Guid.NewGuid(), "agent");
        AgentRunResult second = await RunAsync(root, next, effort: "high");
        Assert.Equal(AgentRunStatus.Completed, second.Status);
        Assert.Equal("gpt-4", second.Settings!.Model.Id);
        Assert.Equal("high", second.Settings.ReasoningEffort);
        await using ServiceProvider restarted = database.BuildRoot();
        DialogSnapshot saved = await ReadAsync(restarted, token);
        Assert.Equal("low", saved.Selection!.Effort);
        Assert.Equal("gpt-5", saved.Turns[0].Settings!.Model);
        Assert.Equal("high", saved.Turns[0].Settings!.Effort);
        Assert.Equal("gpt-4", saved.Turns[1].Settings!.Model);
        Assert.Equal("high", saved.Turns[1].Settings!.Effort);
        using IServiceScope statusScope = root.CreateScope();
        DialogStatus status = Success(await statusScope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(next));
        Assert.Equal("gpt-4", status.SelectedModel);
        Assert.Equal("low", status.SelectedEffort);
        Assert.Equal("actual-server", status.ServerModel);
        Assert.True(status.CanContinue);
        Assert.DoesNotContain("synthetic-secret", JsonSerializer.Serialize(status));
    }

    /// <summary>SQL SaveChanges выполняется, но отказ до commit откатывает новую/изменённую строку выбора.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task ActualRollbackPreservesOriginalSelection(DatabaseProvider provider, bool existing)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        if (existing)
        {
            using IServiceScope seed = database.Root.CreateScope();
            Success(await seed.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, 0, Settings("gpt-5", "high")));
        }
        await using ServiceProvider failure = database.BuildRoot(customize: services =>
        {
            services.AddScoped<ThrowAfterSaveSession>();
            services.AddScoped<IUnitOfWorkSession>(sp => sp.GetRequiredService<ThrowAfterSaveSession>());
        });
        using IServiceScope scope = failure.CreateScope();
        await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<IDialogSettingsWriter>()
            .SaveAsync(Access(token), token, existing ? 1 : 0, Settings("gpt-4", "low")));
        Assert.True(scope.ServiceProvider.GetRequiredService<ThrowAfterSaveSession>().SavedEntries > 0);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
        DialogSnapshot saved = await ReadAsync(database.Root, token);
        Assert.Equal(existing ? "gpt-5" : null, saved.Selection?.Model);
        Assert.Equal(existing ? 1 : 0, saved.Selection?.Version ?? 0);
        Assert.Equal(token.Revision, saved.Token.Revision);
    }

    /// <summary>Owner/expiry/recreate guards не позволяют late выбору создать состояние новой incarnation; cascade удаляет settings.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task GuardsAndCascadeProtectDialogLifetime(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        using IServiceScope scope = database.Root.CreateScope();
        IDialogSettingsWriter writer = scope.ServiceProvider.GetRequiredService<IDialogSettingsWriter>();
        Assert.Equal(ServiceErrorType.Forbidden, (await writer.SaveAsync(new(token.DialogId, DialogOwnerId.From("other"), NOW), token, 0, Settings("gpt-5", "high"))).Error!.Type);
        Assert.Equal(ServiceErrorType.Expired, (await writer.SaveAsync(new(token.DialogId, OWNER, NOW.AddHours(36)), token, 0, Settings("gpt-5", "high"))).Error!.Type);
        Success(await writer.SaveAsync(Access(token), token, 0, Settings("gpt-5", "high")));
        Assert.True((await scope.ServiceProvider.GetRequiredService<IDialogDeletion>().DeleteAsync(Access(token), token)).Success);
        Assert.Empty(await database.QueryAsync("SELECT * FROM \"DialogSettings\""));
        Assert.Equal(ServiceErrorType.NotFound, (await writer.SaveAsync(Access(token), token, 1, Settings("gpt-4", "low"))).Error!.Type);
        DialogWriteToken recreated = await PersistenceIntegrationTests.CreateAsync(database, token.DialogId);
        Assert.Equal(ServiceErrorType.Conflict, (await writer.SaveAsync(Access(token), token, 0, Settings("gpt-4", "low"))).Error!.Type);
        Assert.Null((await ReadAsync(database.Root, recreated)).Selection);
    }

    /// <summary>Два actual writers с одной CAS версией не могут оба commit; driver serialization не маскируется retry.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task ConcurrentSettingsWritersCannotBothCommit(DatabaseProvider provider, bool existing)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        if (existing)
        {
            using IServiceScope seed = database.Root.CreateScope();
            Success(await seed.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, 0, Settings("gpt-5", "high")));
        }
        ConcurrentReadBarrier barrier = new();
        await using ServiceProvider root = database.BuildRoot(customize: services =>
        {
            if (provider == DatabaseProvider.PostgreSql) services.AddDbContext<AgentBridgeDbContext>(options => options.AddInterceptors(barrier));
        });
        using IServiceScope left = root.CreateScope();
        using IServiceScope right = root.CreateScope();
        Task<(ServiceResult<DialogModelSelection>? Result, Exception? Error)> a = Task.Run(() => AttemptAsync(left, "gpt-4"));
        Task<(ServiceResult<DialogModelSelection>? Result, Exception? Error)> b = Task.Run(() => AttemptAsync(right, "gpt-5"));
        (ServiceResult<DialogModelSelection>? Result, Exception? Error)[] outcomes = await Task.WhenAll(a, b);
        Assert.Single(outcomes, outcome => outcome.Result?.Success == true);
        (ServiceResult<DialogModelSelection>? Result, Exception? Error) loser = Assert.Single(outcomes, outcome => outcome.Result?.Success != true);
        if (loser.Error is null) Assert.Equal(ServiceErrorType.Conflict, loser.Result!.Error!.Type);
        else
        {
            Exception driver = loser.Error;
            while (driver.InnerException is not null) driver = driver.InnerException;
            Assert.True(provider == DatabaseProvider.SQLite ? driver is SqliteException { SqliteErrorCode: 5 or 6 }
                : driver is PostgresException { SqlState: "40001" }, loser.Error.ToString());
        }
        DialogSnapshot saved = await ReadAsync(database.Root, token);
        Assert.Equal(existing ? 2 : 1, saved.Selection!.Version);
        Assert.Equal(0, saved.Token.Revision);

        async Task<(ServiceResult<DialogModelSelection>?, Exception?)> AttemptAsync(IServiceScope scope, string model)
        {
            try { return (await scope.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, existing ? 1 : 0, Settings(model, "low")), null); }
            catch (Exception error) { return (null, error); }
        }
    }

    /// <summary>Settings/turn snapshot/compact provenance round-trip; Down/Up сохраняют canonical historical rows без выдуманных defaults.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task NewMigrationRoundTripsProvenanceAndKeepsHistoricalRows(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        Guid turnId = Guid.NewGuid();
        using (IServiceScope scope = database.Root.CreateScope())
        {
            TurnModelSettings pinned = TurnModelSettings.From(Settings("gpt-5", "high"));
            token = Success(await scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>().BeginWithSettingsAsync(Access(token), token, turnId, [Message()], pinned));
            token = Success(await scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>().FinishAsync(Access(token), token, turnId, DialogTurnStatus.Completed, [], []));
            token = Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>().SaveWithModelAsync(Access(token), token, 1,
                ModelResponse.Completed([Message()], new(JsonSerializer.SerializeToElement(new { model = "actual-compact", future = "preserved" }))), "gpt-5"));
            Success(await scope.ServiceProvider.GetRequiredService<IDialogSettingsWriter>().SaveAsync(Access(token), token, 0, Settings("gpt-5", "high")));
        }
        DialogSnapshot saved = await ReadAsync(database.Root, token);
        Assert.Equal("gpt-5", saved.Turns[0].Settings!.Model);
        Assert.Equal("gpt-5", saved.ActiveContext!.SelectedModel);
        Assert.Equal("actual-compact", saved.ActiveContext.Compaction.Envelope!.Content.GetProperty("model").GetString());
        string previous = provider == DatabaseProvider.SQLite ? "20261004074344_AddDurableToolAttempts" : "20261004074347_AddDurableToolAttempts";
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IUnitOfWorkContext<AgentBridgeContextKey> context = scope.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>();
            await context.Database.GetService<IMigrator>().MigrateAsync(previous);
            await context.Database.GetService<IMigrator>().MigrateAsync();
            Assert.False(context.Database.HasPendingModelChanges());
        }
        DialogSnapshot legacy = await ReadAsync(database.Root, token);
        Assert.Null(legacy.Selection);
        Assert.Null(legacy.Turns[0].Settings);
        Assert.Null(legacy.ActiveContext!.SelectedModel);
        Assert.Equal(saved.Turns[0].Items[0].Content.GetRawText(), legacy.Turns[0].Items[0].Content.GetRawText());
        Assert.Equal(saved.ActiveContext.Compaction.Envelope.Content.GetRawText(), legacy.ActiveContext.Compaction.Envelope!.Content.GetRawText());
        Assert.Equal(saved.ExpiresAtUtc, legacy.ExpiresAtUtc);
        Assert.Equal(saved.ContentBytes, legacy.ContentBytes);
        Assert.Equal(saved.Token.Revision, legacy.Token.Revision);
    }

    private static DialogAccess Access(DialogWriteToken token) => new(token.DialogId, OWNER, NOW);
    private static T Success<T>(ServiceResult<T> result) where T : class { Assert.True(result.Success, result.Error?.Message); return result.Data!; }
    private static ModelSettingsSnapshot Settings(string model, string effort) => new(Capability(model), effort, 32_000, 4096);
    private static ModelCapabilities Capability(string model) => new(model, true, 200_000, 200_000, 4096, ["high", "low"], "high", ["text"], true, true, true, true, false);
    private static CanonicalModelItem Message() => new(JsonSerializer.SerializeToElement(new { type = "message", role = "assistant", content = "текст" }));
    private static async Task<DialogSnapshot> ReadAsync(ServiceProvider root, DialogWriteToken token)
    {
        using IServiceScope scope = root.CreateScope();
        return Success(await scope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(token)));
    }
    private static async Task<AgentRunResult> RunAsync(ServiceProvider root, ApplicationCallContext call, string? effort = null)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(new(call, [Message()], [], new(8, 8, 1, TimeSpan.FromMinutes(1)), effort: effort));
    }
    private static ServiceProvider BuildRoot(IntegrationDatabase database, Probe probe) => database.BuildRoot(customize: services =>
    {
        services.AddSingleton(probe);
        services.AddSingleton<TimeProvider>(new Clock());
        services.AddSingleton<IModelSettingsReader, Models>();
        services.AddSingleton<IModelGateway, Gateway>();
        services.AddSingleton<IModelAccessResolver, AccessResolver>();
        services.AddScoped<ContextBuilder>(_ => new([]));
        services.AddAgentBridgeConfiguration(options => { options.Instructions = "test instructions"; options.InstructionsSource = AgentInstructionsSource.Configuration; options.MaxToolSteps = 8; },
            options => { options.RetentionPeriod = TimeSpan.FromDays(7); options.SoftContentLimitBytes = 10_485_760; },
            options => { options.TokenThreshold = 32_000; options.InputTokenReserve = 4_096; options.MaxPasses = 3; });
        services.AddAgentBridgeTokenization();
        services.AddAgentBridgeTools();
        services.AddAgentBridgeRunner();
        services.AddAgentBridgeSettings();
    });
    private class Probe
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Generations;
    }
    private class Clock : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => NOW;
    }
    /// <inheritdoc/>
    private class Models : IModelSettingsReader
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken ct = default) =>
            Task.FromResult(ModelSelectionValidator.Validate(new([Capability("gpt-5"), Capability("gpt-4")]), model ?? "gpt-5", effort ?? "high", 32_000, 4096));
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access, string? model = null, string? effort = null, CancellationToken ct = default) => ReadAsync(ownerId, model, effort, ct);
    }
    /// <inheritdoc/>
    private class AccessResolver : IModelAccessResolver
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default) => Task.FromResult(ServiceResult<ModelAccess>.Ok(new("synthetic-secret")));
    }
    /// <inheritdoc/>
    private class Gateway(Probe probe) : IModelGateway
    {
        /// <inheritdoc/>
        public async Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref probe.Generations) == 1)
            {
                probe.Entered.TrySetResult();
                await probe.Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return ServiceResult<ModelResponse>.Ok(ModelResponse.Completed([Message()], new(JsonSerializer.SerializeToElement(new { model = "actual-server", raw = "synthetic-secret" }))));
        }
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Compact не ожидается в тесте settings.");
    }
}
