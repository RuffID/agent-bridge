using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Адресные durable/runner20 проверки настоящих SQLite/PostgreSQL через EFCoreLibrary0.0.5.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class AgentRunnerIntegrationTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly ModelToolDefinition TOOL = new("action", "Тестовое действие", JsonSerializer.SerializeToElement(new { type = "object" }), false);

    /// <summary>Start виден новому scope до handler; repeated call_id и full payload восстанавливаются новым root.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task PublicRunnerCommitsStartsBeforeActionsAndRoundTripsRepeatedCalls(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        Probe probe = new(token);
        probe.Responses.Enqueue(ModelResponse.Completed([Message("unknown-preserved"), Call("same")],
            new(JsonSerializer.SerializeToElement(new { future = new[] { 1, 2 }, id = "response-one" }))));
        probe.Responses.Enqueue(ModelResponse.Completed([Call("same")]));
        probe.Responses.Enqueue(ModelResponse.Completed([Message("done")]));
        await using ServiceProvider root = BuildRoot(database, probe);
        probe.Root = root;
        AgentRunResult result = await RunAsync(root, probe);
        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(2, probe.Actions);
        Assert.Equal(1, probe.ProviderCalls);
        Assert.Equal(1, probe.AccessCalls);
        await using ServiceProvider restarted = database.BuildRoot();
        DialogSnapshot read = await ReadAsync(restarted, probe.Call);
        Assert.Equal(result.Token!.Revision, read.Token.Revision);
        Assert.Equal(DialogTurnStatus.Completed, read.Turns[0].Status);
        Assert.Equal(1, read.Turns[0].ModelSteps[0].ToolAttempts[0].OutputIndex);
        Assert.Equal("agent", read.Turns[0].ModelSteps[0].ToolAttempts[0].AgentId);
        Assert.All(read.Turns[0].ModelSteps.Take(2), step => Assert.Equal(ToolAttemptState.Succeeded, step.ToolAttempts[0].State));
        Assert.Equal("response-one", read.Turns[0].ModelSteps[0].Response.Envelope!.Content.GetProperty("id").GetString());
        Assert.Equal(2, read.Turns[0].Items.Count(item => Type(item) == "function_call_output"));
        Assert.True(read.ContentBytes > 0);
    }

    /// <summary>Восстановленный Started и legacy null запрещают повтор всего turn/model независимо от call_id.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task RestartDoesNotReplayStartedOrLegacyTurn(DatabaseProvider provider, bool durable)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        Probe probe = new(token);
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([Call("x")]));
        token = await SeedStepAsync(database.Root, probe.Call, token, step);
        ToolExecutionIdentity identity = await CaptureAsync(database.Root, probe.Call, token, step);
        if (durable)
        {
            using IServiceScope start = database.Root.CreateScope();
            token = Success(await start.ServiceProvider.GetRequiredService<IDialogToolAttemptWriter>().StartAsync(Access(probe.Call), token, identity));
        }
        await using ServiceProvider root = BuildRoot(database, probe);
        probe.Root = root;
        AgentRunResult result = await RunAsync(root, probe);
        Assert.Equal(AgentRunStatus.Interrupted, result.Status);
        Assert.Equal(0, probe.Actions);
        Assert.Equal(0, probe.Generations);
        Assert.Equal(token.Revision, result.Token!.Revision);
        StoredModelStep restored = result.Turn!.ModelSteps[0];
        Assert.Equal(step.StepId, restored.StepId);
        if (durable) Assert.Equal(ToolAttemptState.Started, Assert.Single(restored.ToolAttempts).State);
        else Assert.Empty(restored.ToolAttempts);
        Assert.Equal(step.Response.Output[0].Content.GetRawText(), restored.Response.Output[0].Content.GetRawText());
        Assert.DoesNotContain(result.Turn.Items, item => Type(item) == "function_call_output");
    }

    /// <summary>Ошибка после реального SQL-save outcomes откатывает journal и canonical outputs вместе.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task OutcomeSaveFailureRollsBackOutputsAndJournalTogether(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        Probe probe = new(await PersistenceIntegrationTests.CreateAsync(database)) { FailOutcomes = true };
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        await using ServiceProvider root = BuildRoot(database, probe);
        probe.Root = root;
        await Assert.ThrowsAsync<IOException>(() => RunAsync(root, probe));
        Assert.Equal(1, probe.Actions);
        Assert.True(probe.FailedSavedEntries >= 3);
        DialogSnapshot saved = await ReadAsync(database.Root, probe.Call);
        Assert.Equal(DialogTurnStatus.InProgress, saved.Turns[0].Status);
        Assert.Equal(ToolAttemptState.Started, saved.Turns[0].ModelSteps[0].ToolAttempts[0].State);
        Assert.DoesNotContain(saved.Turns[0].Items, item => Type(item) == "function_call_output");
        await using ServiceProvider restarted = BuildRoot(database, probe);
        Assert.Equal(AgentRunStatus.Interrupted, (await RunAsync(restarted, probe)).Status);
        Assert.Equal(1, probe.Actions);
    }

    /// <summary>Реальный start commit с потерянным подтверждением не запускает handler и не refresh/retry после restart.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task UnknownStartCommitPersistsBarrierWithoutExecutingAction(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        Probe probe = new(await PersistenceIntegrationTests.CreateAsync(database)) { FailStartAcknowledgement = true };
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        await using ServiceProvider root = BuildRoot(database, probe);
        probe.Root = root;
        Exception error = await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(root, probe));
        Assert.Contains("lost start acknowledgement", error.ToString());
        Assert.Equal(0, probe.Actions);
        Assert.Equal(1, probe.Generations);
        DialogSnapshot saved = await ReadAsync(database.Root, probe.Call);
        Assert.Equal(DialogTurnStatus.InProgress, saved.Turns[0].Status);
        Assert.Equal(ToolAttemptState.Started, saved.Turns[0].ModelSteps[0].ToolAttempts[0].State);
        await using ServiceProvider restarted = BuildRoot(database, probe);
        Assert.Equal(AgentRunStatus.Interrupted, (await RunAsync(restarted, probe)).Status);
        Assert.Equal(0, probe.Actions);
        Assert.Equal(1, probe.Generations);
    }

    /// <summary>Каждый Start проверяет actual owner/incarnation/revision/expiry/duplicate parent-aware identity.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, "owner", ServiceErrorType.Forbidden)]
    [InlineData(DatabaseProvider.SQLite, "incarnation", ServiceErrorType.Conflict)]
    [InlineData(DatabaseProvider.SQLite, "revision", ServiceErrorType.Conflict)]
    [InlineData(DatabaseProvider.SQLite, "expiry", ServiceErrorType.Expired)]
    [InlineData(DatabaseProvider.SQLite, "duplicate", ServiceErrorType.Conflict)]
    [InlineData(DatabaseProvider.PostgreSql, "owner", ServiceErrorType.Forbidden)]
    [InlineData(DatabaseProvider.PostgreSql, "incarnation", ServiceErrorType.Conflict)]
    [InlineData(DatabaseProvider.PostgreSql, "revision", ServiceErrorType.Conflict)]
    [InlineData(DatabaseProvider.PostgreSql, "expiry", ServiceErrorType.Expired)]
    [InlineData(DatabaseProvider.PostgreSql, "duplicate", ServiceErrorType.Conflict)]
    public async Task DurableStartEnforcesGuards(DatabaseProvider provider, string boundary, ServiceErrorType expected)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        Probe probe = new(token);
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([Call("x")]));
        token = await SeedStepAsync(database.Root, probe.Call, token, step);
        ToolExecutionIdentity identity = await CaptureAsync(database.Root, probe.Call, token, step);
        DialogAccess access = Access(probe.Call);
        switch (boundary)
        {
            case "owner": access = new(probe.Call.DialogId, DialogOwnerId.From("other"), NOW); break;
            case "incarnation": token = new(token.DialogId, Guid.NewGuid(), token.Revision); break;
            case "revision": token = new(token.DialogId, token.IncarnationId, token.Revision - 1); break;
            case "expiry": access = new(probe.Call.DialogId, probe.Call.OwnerId, NOW.AddHours(36)); break;
            case "duplicate":
                using (IServiceScope first = database.Root.CreateScope())
                    token = Success(await first.ServiceProvider.GetRequiredService<IDialogToolAttemptWriter>().StartAsync(access, token, identity));
                break;
        }
        string before = await database.FingerprintAsync();
        using IServiceScope scope = database.Root.CreateScope();
        ServiceResult<DialogWriteToken> refused = await scope.ServiceProvider.GetRequiredService<IDialogToolAttemptWriter>().StartAsync(access, token, identity);
        Assert.Equal(expected, refused.Error!.Type);
        Assert.Equal(before, await database.FingerprintAsync());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().ChangeTracker.Entries());
    }

    /// <summary>Delete/cleanup/expiry после действия отклоняют late outcomes и не восстанавливают удалённые строки.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, "delete", ServiceErrorType.NotFound)]
    [InlineData(DatabaseProvider.SQLite, "cleanup", ServiceErrorType.NotFound)]
    [InlineData(DatabaseProvider.SQLite, "expiry", ServiceErrorType.Expired)]
    [InlineData(DatabaseProvider.PostgreSql, "delete", ServiceErrorType.NotFound)]
    [InlineData(DatabaseProvider.PostgreSql, "cleanup", ServiceErrorType.NotFound)]
    [InlineData(DatabaseProvider.PostgreSql, "expiry", ServiceErrorType.Expired)]
    public async Task LateOutcomeAfterDeleteCleanupOrExpiryIsRefused(DatabaseProvider provider, string boundary, ServiceErrorType expected)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        Probe probe = new(await PersistenceIntegrationTests.CreateAsync(database));
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        probe.Action = async (_, _) =>
        {
            DialogSnapshot current = await ReadAsync(database.Root, probe.Call);
            using IServiceScope deletion = database.Root.CreateScope();
            if (boundary == "delete") Assert.True((await deletion.ServiceProvider.GetRequiredService<IDialogDeletion>().DeleteAsync(Access(probe.Call), current.Token)).Success);
            else
            {
                probe.Clock.Now = current.ExpiresAtUtc;
                if (boundary == "cleanup") Assert.True((await deletion.ServiceProvider.GetRequiredService<IExpiredDialogDeletion>().DeleteAsync(current.Token, probe.Clock.Now)).Success);
            }
            return Confirmed();
        };
        await using ServiceProvider root = BuildRoot(database, probe);
        probe.Root = root;
        AgentRunResult result = await RunAsync(root, probe);
        Assert.Equal(expected, result.Error!.Type);
        Assert.False(result.TerminalSaved);
        Assert.Single(result.LastTools!.Outputs);
        Assert.Equal(1, probe.Actions);
        if (boundary != "expiry")
        {
            foreach (string table in new[] { "Dialogs", "DialogTurns", "ModelSteps", "CanonicalItems", "DialogContexts" })
                Assert.Equal(0L, Convert.ToInt64(Assert.Single(await database.QueryAsync("SELECT count(*) AS count FROM \"" + table + "\""))["count"]));
        }
    }

    /// <summary>Parallel checkpoint scopes сериализуются; Unknown соседа и confirmed output сохраняются до exception.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ParallelPartialFailurePersistsConfirmedNeighbor(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        Probe probe = new(await PersistenceIntegrationTests.CreateAsync(database));
        probe.Responses.Enqueue(ModelResponse.Completed([Call("fail"), Call("ok")]));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Action = async (invocation, cancellationToken) =>
        {
            if (invocation.CallId == "fail") { await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); throw new InvalidOperationException("partial action failure"); }
            entered.TrySetResult();
            return Confirmed();
        };
        await using ServiceProvider root = BuildRoot(database, probe);
        probe.Root = root;
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(root, probe));
        DialogSnapshot saved = await ReadAsync(database.Root, probe.Call);
        Assert.Equal(DialogTurnStatus.Incomplete, saved.Turns[0].Status);
        Assert.Equal([ToolAttemptState.Unknown, ToolAttemptState.Succeeded], saved.Turns[0].ModelSteps[0].ToolAttempts.Select(item => item.State));
        Assert.Single(saved.Turns[0].Items, item => Type(item) == "function_call_output");
        Assert.Equal(2, probe.Actions);
    }

    /// <summary>Предыдущая схема с историческими steps проходит Up; Down/Up сохраняет canonical rows, null не даёт replay.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task MigrationDownAndUpgradePreserveHistoricalRows(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        Probe probe = new(token);
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([Call("legacy")]));
        token = await SeedStepAsync(database.Root, probe.Call, token, step);
        ToolExecutionIdentity identity = await CaptureAsync(database.Root, probe.Call, token, step);
        using (IServiceScope start = database.Root.CreateScope())
            token = Success(await start.ServiceProvider.GetRequiredService<IDialogToolAttemptWriter>().StartAsync(Access(probe.Call), token, identity));
        string initial = provider == DatabaseProvider.SQLite ? "20261003155233_InitialAgentBridgeSchema" : "20261003155235_InitialAgentBridgeSchema";
        using (IServiceScope down = database.Root.CreateScope())
            await down.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>().Database.GetService<IMigrator>().MigrateAsync(initial);
        IReadOnlyDictionary<string, object?> legacy = Assert.Single(await database.QueryAsync("SELECT \"ResponseOutputJson\" FROM \"ModelSteps\""));
        Assert.Contains("legacy", (string)legacy["ResponseOutputJson"]!);
        using (IServiceScope upgrade = database.Root.CreateScope())
        {
            IUnitOfWorkContext<AgentBridgeContextKey> context = upgrade.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>();
            await context.Database.GetService<IMigrator>().MigrateAsync();
            Assert.False(context.Database.HasPendingModelChanges());
        }
        DialogSnapshot saved = await ReadAsync(database.Root, probe.Call);
        Assert.Empty(saved.Turns[0].ModelSteps[0].ToolAttempts);
        Assert.Equal(step.Response.Output[0].Content.GetRawText(), saved.Turns[0].ModelSteps[0].Response.Output[0].Content.GetRawText());
        Assert.Equal(token.Revision, saved.Token.Revision);
        await using ServiceProvider restarted = BuildRoot(database, probe);
        Assert.Equal(AgentRunStatus.Interrupted, (await RunAsync(restarted, probe)).Status);
        Assert.Equal(0, probe.Actions);
    }

    private static ServiceProvider BuildRoot(IntegrationDatabase database, Probe probe) => database.BuildRoot(customize: services =>
    {
        services.AddSingleton(probe);
        services.AddSingleton<TimeProvider>(probe.Clock);
        services.AddSingleton<IModelGateway, Gateway>();
        services.AddSingleton<IModelSettingsReader, Settings>();
        services.AddSingleton<IModelAccessResolver, Resolver>();
        services.AddSingleton<IContextTokenCounter, Counter>();
        services.AddSingleton<IContextProvider, Provider>();
        services.AddScoped<ContextBuilder>(sp => new([sp.GetRequiredService<IContextProvider>()]));
        services.Configure<AgentOptions>(options => options.Instructions = "fixed");
        services.Configure<ContextCompactionOptions>(options => options.MaxPasses = 2);
        services.AddAgentBridgeTool<Handler, Validator>(TOOL);
        services.AddAgentBridgeRunner();
        if (probe.FailOutcomes || probe.FailStartAcknowledgement) services.AddScoped<IUnitOfWorkSession>(sp => new OutcomeFailureSession(
            sp.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>(), sp.GetRequiredService<AgentBridgeDbContext>(), probe));
    });
    private static async Task<AgentRunResult> RunAsync(ServiceProvider root, Probe probe)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(new(probe.Call, [Message("input")], [TOOL.Name], new(8, 8, 2, TimeSpan.FromMinutes(1))));
    }
    private static async Task<DialogSnapshot> ReadAsync(ServiceProvider root, ApplicationCallContext call)
    {
        using IServiceScope scope = root.CreateScope();
        ServiceResult<DialogSnapshot> result = await scope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(Access(call));
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!;
    }
    private static async Task<DialogWriteToken> SeedStepAsync(ServiceProvider root, ApplicationCallContext call, DialogWriteToken token, StoredModelStep step)
    {
        using IServiceScope scope = root.CreateScope();
        IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
        token = Success(await writer.BeginAsync(Access(call), token, call.TurnId, []));
        return Success(await writer.AppendAsync(Access(call), token, call.TurnId, step.Response.Output, [step]));
    }
    private static async Task<ToolExecutionIdentity> CaptureAsync(ServiceProvider root, ApplicationCallContext call, DialogWriteToken token, StoredModelStep step)
    {
        // Captured identity принадлежит actual executor; неизвестный tool не дошёл бы до checkpoint.
        using ServiceProvider registryRoot = BuildCaptureRoot();
        IToolExecutor executor = registryRoot.GetRequiredService<IToolExecutor>();
        Capture checkpoint = new();
        ToolExecutionSession session = executor.CreateSession(call, token, NOW.AddHours(36), [TOOL.Name], new(8, 8, 1, TimeSpan.FromMinutes(1)), checkpoint);
        await executor.ExecuteAsync(session, step);
        return checkpoint.Identity ?? throw new InvalidOperationException("Checkpoint не получил identity.");
    }
    private static ServiceProvider BuildCaptureRoot()
    {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(new Clock());
        services.AddAgentBridgeTool<CaptureHandler, Validator>(TOOL);
        return services.BuildServiceProvider();
    }
    private static DialogAccess Access(ApplicationCallContext call) => new(call.DialogId, call.OwnerId, NOW);
    private static DialogWriteToken Success(ServiceResult<DialogWriteToken> result) { Assert.True(result.Success, result.Error?.Message); return result.Data!; }
    private static CanonicalModelItem Message(string text) => new(JsonSerializer.SerializeToElement(new { type = "message", role = "user", content = text, future = true }));
    private static CanonicalModelItem Call(string id) => new(JsonSerializer.SerializeToElement(new { type = "function_call", call_id = id, name = "action", arguments = "{}", future = new[] { 1, 2 } }));
    private static string? Type(CanonicalModelItem item) => item.Content.TryGetProperty("type", out JsonElement type) ? type.GetString() : null;
    private static ServiceResult<ToolOutput> Confirmed() => ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { confirmed = true })));
    private class Probe(DialogWriteToken token)
    {
        public ApplicationCallContext Call { get; } = new(token.DialogId, DialogOwnerId.From(" User:Б "), Guid.NewGuid(), "agent");
        public Clock Clock { get; } = new();
        public ServiceProvider? Root;
        public Queue<ModelResponse> Responses { get; } = new();
        public ModelAccess Access { get; } = new("test-only-access");
        public int Actions, Generations, ProviderCalls, AccessCalls, FailedSavedEntries;
        public bool FailOutcomes;
        public bool FailStartAcknowledgement;
        public Func<ToolInvocation, CancellationToken, Task<ServiceResult<ToolOutput>>> Action = (_, _) => Task.FromResult(Confirmed());
    }
    private class Clock : TimeProvider { public DateTimeOffset Now = NOW; public override DateTimeOffset GetUtcNow() => Now; }
    /// <inheritdoc/>
    private class Handler(Probe probe, AgentBridgeDbContext handlerContext) : IToolHandler
    {
        /// <inheritdoc/>
        public ModelToolDefinition Definition => TOOL;
        /// <inheritdoc/>
        public async Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            Assert.Null(handlerContext.Database.CurrentTransaction);
            Assert.Empty(handlerContext.ChangeTracker.Entries());
            using IServiceScope observation = probe.Root!.CreateScope();
            // Один parent-aware base query наблюдает только committed step. Защищённый multi-read snapshot
            // имеет право отклониться при параллельной записи соседнего checkpoint, поэтому не служит oracle здесь.
            ModelStepRecord step = (await observation.ServiceProvider.GetRequiredService<ModelStepRecordQueries>()
                .ReadTurnAsync(invocation.Call.DialogId.Value, invocation.Call.TurnId)).Last();
            ModelResponse response = step.Response.ToModelResponse();
            int position = response.Output.ToList().FindIndex(item => Type(item) == "function_call"
                && item.Content.GetProperty("call_id").GetString() == invocation.CallId);
            using JsonDocument journal = JsonDocument.Parse(step.ToolAttemptsJson!);
            Assert.Contains(journal.RootElement.GetProperty("attempts").EnumerateArray(),
                attempt => attempt.GetProperty("position").GetInt32() == position
                    && attempt.GetProperty("state").GetInt32() == (int)ToolAttemptState.Started);
            Interlocked.Increment(ref probe.Actions);
            return await probe.Action(invocation, cancellationToken);
        }
    }
    /// <inheritdoc/>
    private class Validator : IToolInvocationValidator
    {
        /// <inheritdoc/>
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation, CancellationToken cancellationToken = default) => Task.FromResult(ServiceResult.Ok());
    }
    /// <inheritdoc/>
    private class Capture : IToolExecutionCheckpoint
    {
        public ToolExecutionIdentity? Identity;
        /// <inheritdoc/>
        public Task<ServiceResult> BeforeExecuteAsync(ToolExecutionIdentity identity, ToolInvocation invocation, CancellationToken cancellationToken = default)
        { Identity = identity; return Task.FromResult(ServiceResult.Fail(new(ServiceErrorType.Conflict, "capture-only"))); }
    }
    /// <inheritdoc/>
    private class CaptureHandler : IToolHandler
    {
        /// <inheritdoc/>
        public ModelToolDefinition Definition => TOOL;
        /// <inheritdoc/>
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Capture не разрешает handler.");
    }
    /// <inheritdoc/>
    private class Gateway(Probe probe) : IModelGateway
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        { Assert.Same(probe.Access, access); probe.Generations++; return Task.FromResult(ServiceResult<ModelResponse>.Ok(probe.Responses.Dequeue())); }
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Budget ниже threshold.");
    }
    /// <inheritdoc/>
    private class Settings(Probe probe) : IModelSettingsReader
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken ct = default) => throw new InvalidOperationException("Run требует pinned access.");
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access, string? model = null, string? effort = null, CancellationToken ct = default)
        { Assert.Same(probe.Access, access); return Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Ok(new(new("gpt-5", true, 1000, 1000, 100, ["high"], "high", ["text"], true, true, true, true, false), "high", 900, 10))); }
    }
    /// <inheritdoc/>
    private class Resolver(Probe probe) : IModelAccessResolver
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default) { probe.AccessCalls++; return Task.FromResult(ServiceResult<ModelAccess>.Ok(probe.Access)); }
    }
    /// <inheritdoc/>
    private class Counter : IContextTokenCounter
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default) => Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new("test", 1, 1, false)));
    }
    /// <inheritdoc/>
    private class Provider(Probe probe) : IContextProvider
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default) { probe.ProviderCalls++; return Task.FromResult(ServiceResult<ContextContribution>.Ok(new([]))); }
    }
    /// <inheritdoc/>
    private class OutcomeFailureSession(IUnitOfWorkContext<AgentBridgeContextKey> context, AgentBridgeDbContext db, Probe probe) : IUnitOfWorkSession
    {
        private readonly EfUnitOfWorkSession _inner = new(context);
        private bool _started;
        /// <inheritdoc/>
        public async Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken)
        {
            IDbContextTransaction transaction = await _inner.BeginAsync(cancellationToken);
            return probe.FailStartAcknowledgement ? new LostAcknowledgementTransaction(transaction, () => _started) : transaction;
        }
        /// <inheritdoc/>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            bool outcomes = db.ChangeTracker.Entries<ModelStepRecord>().Any(entry => entry.Entity.ToolAttemptsJson?.Contains("\"state\":1", StringComparison.Ordinal) == true);
            _started = db.ChangeTracker.Entries<ModelStepRecord>().Any(entry => entry.Entity.ToolAttemptsJson?.Contains("\"state\":0", StringComparison.Ordinal) == true);
            int count = await _inner.SaveChangesAsync(cancellationToken);
            if (probe.FailOutcomes && outcomes) { probe.FailedSavedEntries = count; throw new IOException("failure after real outcome SQL save"); }
            return count;
        }
        /// <inheritdoc/>
        public void Clear() => _inner.Clear();
    }

    /// <inheritdoc/>
    private class LostAcknowledgementTransaction(IDbContextTransaction inner, Func<bool> started) : IDbContextTransaction
    {
        /// <inheritdoc/>
        public Guid TransactionId => inner.TransactionId;
        /// <inheritdoc/>
        public void Commit() => throw new InvalidOperationException("Тест использует только async commit.");
        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await inner.CommitAsync(cancellationToken);
            if (started()) throw new IOException("lost start acknowledgement after real commit");
        }
        /// <inheritdoc/>
        public void Rollback() => inner.Rollback();
        /// <inheritdoc/>
        public Task RollbackAsync(CancellationToken cancellationToken = default) => inner.RollbackAsync(cancellationToken);
        /// <inheritdoc/>
        public void Dispose() => inner.Dispose();
        /// <inheritdoc/>
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
