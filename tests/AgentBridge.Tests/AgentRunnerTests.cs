using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Public run с actual builder/compactor/guard/executor и isolated storage/gateway doubles.</summary>
public class AgentRunnerTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly ModelToolDefinition TOOL = new("action", "Действие", JsonSerializer.SerializeToElement(new { type = "object" }), false);

    /// <summary>Старый user reader без pinned-access контракта останавливается явно до provider/model/write.</summary>
    [Fact]
    public async Task LegacySettingsReaderFailsWithoutUnpinnedFallback()
    {
        Probe probe = new();
        ServiceCollection services = Services(probe);
        services.AddSingleton<IModelSettingsReader, LegacySettings>();
        await using ServiceProvider root = services.BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Empty(probe.Events);
        Assert.Equal(0, probe.Generations);
        Assert.Equal(0, probe.ProviderCalls);
    }

    /// <summary>Повторные call_id разных шагов допустимы; providers/settings/access фиксируются один раз.</summary>
    [Fact]
    public async Task FullLoopFreezesProvidersAndSettingsAndSavesBeforeHandlers()
    {
        Probe probe = new();
        probe.Responses.Enqueue(ModelResponse.Completed([Call("same")]));
        probe.Responses.Enqueue(ModelResponse.Completed([Call("same")]));
        probe.Responses.Enqueue(ModelResponse.Completed([Message("done")]));
        await using ServiceProvider root = Services(probe).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = root.CreateScope();
        int updates = 0;
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(Request(probe),
            (_, _) => { updates++; return ValueTask.CompletedTask; });
        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(2, probe.Actions);
        Assert.Equal(1, probe.ProviderCalls);
        Assert.Equal(1, probe.AccessCalls);
        Assert.Equal(1, probe.SettingsCalls);
        Assert.Equal(3, updates);
        Assert.Equal(3, result.Turn!.ModelSteps.Count);
        Assert.All(result.Turn.ModelSteps.Take(2), step => Assert.Equal(ToolAttemptState.Succeeded, Assert.Single(step.ToolAttempts).State));
        Assert.Equal(2, result.Turn.Items.Count(item => Type(item) == "function_call_output"));
        Assert.Equal(1, probe.Requests[2].Input.Count(item => item.Content.TryGetProperty("provider", out _)));
        Assert.All(probe.Requests, request => Assert.Equal("instructions", request.Instructions));
        Assert.Equal(probe.Events.Count, probe.Scopes.Distinct().Count());
        Assert.Equal(new[] { "begin", "append", "start", "outcomes", "append", "start", "outcomes", "append", "finish" }, probe.Events);
        Assert.Equal(DialogTurnStatus.Completed, probe.Dialog.Turns[0].Status);
    }

    /// <summary>Existing turn, включая legacy без журнала, никогда не превращается в разрешение replay.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartRefusesExistingTurnAndPreservesHistory(bool started)
    {
        Probe probe = new();
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([Call("x")]),
            started ? [new(0, "agent", ToolAttemptState.Started)] : []);
        probe.Replace([new(probe.Call.TurnId, 1, DialogTurnStatus.InProgress, step.Response.Output, [step])]);
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(AgentRunStatus.Interrupted, result.Status);
        Assert.False(result.TerminalSaved);
        Assert.Equal(0, probe.Actions);
        Assert.Equal(0, probe.Generations);
        Assert.Equal(0, probe.ProviderCalls);
        Assert.Empty(probe.Events);
        Assert.Same(step, result.Turn!.ModelSteps[0]);
    }

    /// <summary>Partial lifecycle сохраняет полный call без fake output; новый turn блокируется context builder.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Incomplete, AgentRunStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Canceled, AgentRunStatus.Canceled)]
    [InlineData(ModelResponseStatus.Failed, AgentRunStatus.Failed)]
    public async Task PartialModelReportIsSavedAndBlocksNextContext(ModelResponseStatus status, AgentRunStatus expected)
    {
        Probe probe = new();
        ModelResponse response = status switch
        {
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete([Call("partial", "{\"a\":")]),
            ModelResponseStatus.Canceled => ModelResponse.Canceled([Call("partial", "{\"a\":")]),
            _ => ModelResponse.Failed([Call("partial", "{\"a\":")], new(ServiceErrorType.Rejected, "safe"))
        };
        probe.Responses.Enqueue(response);
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(expected, result.Status);
        Assert.Same(response, Assert.Single(result.Turn!.ModelSteps).Response);
        Assert.DoesNotContain(result.Turn.Items, item => Type(item) == "function_call_output");
        AgentRunResult next = await RunAsync(root, Request(probe, Guid.NewGuid()));
        Assert.Equal(ServiceErrorType.Conflict, next.Error!.Type);
        Assert.Equal(1, probe.Generations);
        Assert.Equal(0, probe.Actions);
    }

    /// <summary>Неизвестный handler outcome сохраняет Unknown и запрещает следующую генерацию/restart.</summary>
    [Fact]
    public async Task UnknownActionIsPersistedWithoutOutput()
    {
        Probe probe = new();
        probe.Responses.Enqueue(ModelResponse.Completed([Call("unknown")]));
        probe.Action = (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Fail(new(ServiceErrorType.Timeout, "raw private detail")));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(AgentRunStatus.Interrupted, result.Status);
        Assert.Equal(ToolAttemptState.Unknown, result.Turn!.ModelSteps[0].ToolAttempts[0].State);
        Assert.Empty(result.LastTools!.Outputs);
        Assert.Equal(1, probe.Generations);
        Assert.DoesNotContain("raw", result.Error!.Message);
        Assert.Equal(AgentRunStatus.Interrupted, (await RunAsync(root, Request(probe))).Status);
        Assert.Equal(1, probe.Actions);
    }

    /// <summary>Отказ checkpoint/outputs/terminal не становится Completed и не refresh/retry исходный token.</summary>
    [Theory]
    [InlineData("start", 0)]
    [InlineData("outcomes", 1)]
    [InlineData("finish", 1)]
    public async Task WriteRefusalStopsAndKeepsUnsavedToolReport(string rejected, int actions)
    {
        Probe probe = new() { RejectWrite = rejected };
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        probe.Responses.Enqueue(ModelResponse.Completed([Message("done")]));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.False(result.TerminalSaved);
        Assert.Equal(actions, probe.Actions);
        Assert.Equal(1, probe.Events.Count(item => item == rejected));
        if (rejected == "outcomes") Assert.Single(result.LastTools!.Outputs);
        Assert.Equal(1, probe.Reads);
    }

    /// <summary>Exception после start сохраняет honest partial finalization и распространяет исходную ошибку.</summary>
    [Fact]
    public async Task HandlerExceptionPersistsUnknownAndOriginalException()
    {
        Probe probe = new();
        InvalidOperationException primary = new("primary");
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        probe.Action = (_, _) => Task.FromException<ServiceResult<ToolOutput>>(primary);
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        Assert.Same(primary, await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(root, Request(probe))));
        Assert.Equal(DialogTurnStatus.Incomplete, probe.Dialog.Turns[0].Status);
        Assert.Equal(ToolAttemptState.Unknown, probe.Dialog.Turns[0].ModelSteps[0].ToolAttempts[0].State);
    }

    /// <summary>Primary tool/model failure и ошибка записи partial data не затирают друг друга.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialPersistenceFailurePreservesPrimaryAndCleanup(bool model)
    {
        Probe probe = new();
        InvalidOperationException primary = new("primary");
        IOException cleanup = new("cleanup");
        probe.ThrowWrite = model ? "append" : "outcomes";
        probe.WriteException = cleanup;
        if (model) probe.GenerateFailure = primary;
        else
        {
            probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
            probe.Action = (_, _) => Task.FromException<ServiceResult<ToolOutput>>(primary);
        }
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => RunAsync(root, Request(probe), streaming: model));
        Assert.Contains(primary, error.Flatten().InnerExceptions);
        Assert.Contains(cleanup, error.Flatten().InnerExceptions);
        Assert.DoesNotContain("finish", probe.Events);
    }

    /// <summary>Late success после caller cancellation сохраняется; итог Canceled и повтор не разрешён.</summary>
    [Fact]
    public async Task CancellationAfterConfirmedActionSavesOutput()
    {
        Probe probe = new();
        using CancellationTokenSource caller = new();
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        probe.Action = (_, _) => { caller.Cancel(); return Task.FromResult(Success()); };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe), caller.Token);
        Assert.Equal(AgentRunStatus.Canceled, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Single(result.Turn!.Items, item => Type(item) == "function_call_output");
        Assert.Equal(ToolAttemptState.Succeeded, result.Turn.ModelSteps[0].ToolAttempts[0].State);
    }

    /// <summary>Expiry/delete после действия не позволяет записать outcomes или recreate диалог.</summary>
    [Theory]
    [InlineData(false, ServiceErrorType.Expired)]
    [InlineData(true, ServiceErrorType.NotFound)]
    public async Task LateActionWriteFailsAtFreshBoundary(bool deleted, ServiceErrorType expected)
    {
        Probe probe = new();
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        probe.Action = (_, _) =>
        {
            if (deleted) probe.Deleted = true;
            else probe.Clock.Now = probe.Dialog.ExpiresAtUtc;
            return Task.FromResult(Success());
        };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(expected, result.Error!.Type);
        Assert.False(result.TerminalSaved);
        Assert.Single(result.LastTools!.Outputs);
        Assert.Equal(ToolAttemptState.Started, result.Turn!.ModelSteps[0].ToolAttempts[0].State);
        Assert.DoesNotContain("finish", probe.Events);
    }

    /// <summary>Successful compact token сохраняется при последующем exception/OCE без refresh и второго provider I/O.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulCompactSurvivesNextPassExceptionOrCancellation(bool cancel)
    {
        Probe probe = new() { Threshold = 50 };
        probe.Replace([new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message("large", 100)], [])]);
        using CancellationTokenSource caller = new();
        Exception primary = new InvalidOperationException("compact second pass");
        probe.Compact = () =>
        {
            if (probe.Compacts == 1) return Task.FromResult(ServiceResult<ModelResponse>.Ok(ModelResponse.Completed([Message("smaller", 60)])));
            if (cancel) { caller.Cancel(); return Task.FromCanceled<ServiceResult<ModelResponse>>(caller.Token); }
            return Task.FromException<ServiceResult<ModelResponse>>(primary);
        };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        if (cancel) Assert.Equal(AgentRunStatus.Canceled, (await RunAsync(root, Request(probe), caller.Token)).Status);
        else Assert.Same(primary, await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(root, Request(probe))));
        Assert.Equal(1, probe.Dialog.ActiveContext!.Version);
        Assert.Equal(1, probe.Dialog.ActiveContext.ThroughTurnSequence);
        Assert.Equal(1, probe.ProviderCalls);
        Assert.Equal(1, probe.Reads);
        Assert.NotEqual(DialogTurnStatus.InProgress, probe.Dialog.Turns[1].Status);
        Assert.Equal(new[] { "begin", "context", "finish" }, probe.Events);
    }

    /// <summary>Late cancellation сразу после успешного compact save использует принятый token в finalization.</summary>
    [Fact]
    public async Task CancellationImmediatelyAfterCompactSaveKeepsAcceptedToken()
    {
        Probe probe = new() { Threshold = 50 };
        probe.Replace([new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message("large", 100)], [])]);
        using CancellationTokenSource caller = new();
        probe.AfterWrite = kind => { if (kind == "context") caller.Cancel(); };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe), caller.Token);
        Assert.Equal(AgentRunStatus.Canceled, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(1, probe.Dialog.ActiveContext!.Version);
        Assert.Equal(probe.Dialog.Token.Revision, result.Token!.Revision);
        Assert.Equal(0, probe.Generations);
    }

    /// <summary>Compact HTTP отказ не маскируется generation при допустимом full budget.</summary>
    [Fact]
    public async Task CompactFailureRunsSeparateGuardAndStopsGeneration()
    {
        Probe probe = new() { Threshold = 50 };
        probe.Replace([new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message("large", 100)], [])]);
        probe.Compact = () => Task.FromResult(ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Rejected, "compact refused")));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal("compact refused", result.Error!.Message);
        Assert.Equal(0, probe.Generations);
        Assert.True(probe.Counts >= 3);
    }

    /// <summary>Opaque compact сохраняется, но UnknownBudget не разрешает отправить generation.</summary>
    [Fact]
    public async Task OpaqueCompactIsPersistedButFullGuardRefuses()
    {
        Probe probe = new() { Threshold = 50 };
        probe.Replace([new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message("large", 100)], [])]);
        probe.Compact = () => Task.FromResult(ServiceResult<ModelResponse>.Ok(ModelResponse.Completed([Item("{\"type\":\"compaction\",\"encrypted_content\":\"opaque\"}")])));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.NotNull(probe.Dialog.ActiveContext);
        Assert.Equal(0, probe.Generations);
    }

    /// <summary>Caller cancellation во время terminal save не возвращает Completed; accepted terminal не переписывается.</summary>
    [Fact]
    public async Task CancellationDuringTerminalSaveReturnsCanceled()
    {
        Probe probe = new();
        using CancellationTokenSource caller = new();
        probe.Responses.Enqueue(ModelResponse.Completed([]));
        probe.AfterWrite = kind => { if (kind == "finish") caller.Cancel(); };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe), caller.Token);
        Assert.Equal(AgentRunStatus.Canceled, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(DialogTurnStatus.Completed, result.Turn!.Status);
    }

    /// <summary>Parallel partial failure ждёт соседний handler и сохраняет confirmed output до распространения primary.</summary>
    [Fact]
    public async Task ParallelFailureWaitsAndPersistsNeighbor()
    {
        Probe probe = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InvalidOperationException primary = new("primary");
        probe.Responses.Enqueue(ModelResponse.Completed([Call("fail"), Call("slow")]));
        probe.Action = async (invocation, token) =>
        {
            if (invocation.CallId == "fail") { await entered.Task.WaitAsync(token); failed.SetResult(); throw primary; }
            entered.SetResult();
            await release.Task;
            return Success();
        };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        Task<AgentRunResult> run = RunAsync(root, Request(probe));
        try
        {
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            release.SetResult();
            Assert.Same(primary, await Assert.ThrowsAsync<InvalidOperationException>(() => run));
            Assert.Equal([ToolAttemptState.Unknown, ToolAttemptState.Succeeded], probe.Dialog.Turns[0].ModelSteps[0].ToolAttempts.Select(item => item.State));
            Assert.Single(probe.Dialog.Turns[0].Items, item => Type(item) == "function_call_output");
            Assert.Equal(2, probe.Actions);
        }
        finally { release.TrySetResult(); try { await run; } catch (InvalidOperationException) { } }
    }

    /// <summary>Late cancellation до нового Execute не переиспользует LastResult предыдущего StepId.</summary>
    [Fact]
    public async Task CancellationBeforeSecondToolStepDoesNotPersistOldBatchTwice()
    {
        Probe probe = new();
        using CancellationTokenSource caller = new();
        probe.Responses.Enqueue(ModelResponse.Completed([Call("one")]));
        probe.Responses.Enqueue(ModelResponse.Completed([Call("two")]));
        probe.AfterWrite = kind => { if (kind == "append" && probe.Generations == 2) caller.Cancel(); };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe), caller.Token);
        Assert.Equal(AgentRunStatus.Canceled, result.Status);
        Assert.Equal(1, probe.Events.Count(item => item == "outcomes"));
        Assert.Equal(1, probe.Actions);
        Assert.Empty(result.Turn!.ModelSteps[1].ToolAttempts);
    }

    /// <summary>Confirmed tool output входит в новый полный budget; второй model вызов при превышении запрещён.</summary>
    [Fact]
    public async Task GuardChecksWholePayloadAfterToolOutcome()
    {
        Probe probe = new() { LargeToolOutputBudget = true };
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AgentRunResult result = await RunAsync(root, Request(probe));
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(1, probe.Generations);
        Assert.Single(result.Turn!.Items, item => Type(item) == "function_call_output");
        Assert.Equal(1, probe.ProviderCalls);
    }

    /// <summary>Unknown start commit запрещает handler, refresh и дальнейшие writes даже при фактическом обновлении fake storage.</summary>
    [Fact]
    public async Task UnknownCheckpointCommitBlocksActionWithoutTokenRefresh()
    {
        Probe probe = new();
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        IOException failure = new("unknown commit");
        probe.AfterWrite = kind => { if (kind == "start") throw failure; };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => RunAsync(root, Request(probe))));
        Assert.Equal(0, probe.Actions);
        Assert.Equal(1, probe.Reads);
        Assert.Equal(new[] { "begin", "append", "start" }, probe.Events);
        Assert.Equal(ToolAttemptState.Started, probe.Dialog.Turns[0].ModelSteps[0].ToolAttempts[0].State);
    }

    /// <summary>Primary read/write exception и DisposeAsync того же scope сохраняются вместе через public run.</summary>
    [Theory]
    [InlineData("read")]
    [InlineData("append")]
    [InlineData("start")]
    public async Task ScopeCleanupPreservesPrimaryReadOrWriteFailure(string boundary)
    {
        Probe probe = new() { ThrowScopeAt = boundary };
        InvalidOperationException primary = new("primary port");
        IOException cleanup = new("scope cleanup");
        probe.ScopeException = cleanup;
        if (boundary == "read") probe.ReadException = primary;
        else { probe.ThrowWrite = boundary; probe.WriteException = primary; }
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => RunAsync(root, Request(probe)));
        Assert.Contains(primary, error.Flatten().InnerExceptions);
        Assert.Contains(cleanup, error.Flatten().InnerExceptions);
        Assert.Equal(0, probe.Actions);
        Assert.DoesNotContain("finish", probe.Events);
    }

    /// <summary>Успешный save перед Dispose failure остаётся принятой проекцией; дальнейшие writes не повторяются.</summary>
    [Fact]
    public async Task SuccessfulWriteThenScopeDisposeFailureBlocksFurtherWrites()
    {
        Probe probe = new() { ThrowScopeAt = "append" };
        probe.Responses.Enqueue(ModelResponse.Completed([Call("x")]));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        Assert.Same(probe.ScopeException, await Assert.ThrowsAsync<IOException>(() => RunAsync(root, Request(probe))));
        Assert.Equal(2, probe.Dialog.Token.Revision);
        Assert.Single(probe.Dialog.Turns[0].ModelSteps);
        Assert.Equal(new[] { "begin", "append" }, probe.Events);
        Assert.Equal(0, probe.Actions);
    }

    private static async Task<AgentRunResult> RunAsync(ServiceProvider root, AgentRunRequest request, CancellationToken ct = default, bool streaming = false)
    {
        using IServiceScope scope = root.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(request,
            streaming ? (_, _) => ValueTask.CompletedTask : null, ct);
    }
    private static AgentRunRequest Request(Probe probe, Guid? turn = null) => new(
        turn is null ? probe.Call : new(probe.Call.DialogId, probe.Call.OwnerId, turn.Value, "agent"),
        [Message("input")], [TOOL.Name], new(8, 8, 2, TimeSpan.FromMinutes(1)));
    private static ServiceCollection Services(Probe probe)
    {
        ServiceCollection services = new();
        services.AddSingleton(probe);
        services.AddSingleton<TimeProvider>(probe.Clock);
        services.AddScoped<Store>();
        services.AddScoped<IDialogReader>(sp => sp.GetRequiredService<Store>());
        services.AddScoped<IDialogTurnWriter>(sp => sp.GetRequiredService<Store>());
        services.AddScoped<IDialogToolAttemptWriter>(sp => sp.GetRequiredService<Store>());
        services.AddScoped<IDialogContextWriter>(sp => sp.GetRequiredService<Store>());
        services.AddSingleton<IModelSettingsReader, Settings>();
        services.AddSingleton<IModelAccessResolver, Access>();
        services.AddSingleton<IModelGateway, Gateway>();
        services.AddSingleton<IContextTokenCounter, Counter>();
        services.AddSingleton<IContextProvider, Provider>();
        services.AddScoped<ContextBuilder>(sp => new([sp.GetRequiredService<IContextProvider>()]));
        services.Configure<AgentOptions>(options => options.Instructions = "instructions");
        services.Configure<ContextCompactionOptions>(options => options.MaxPasses = 3);
        services.AddAgentBridgeTool<Handler, Validator>(TOOL);
        services.AddAgentBridgeRunner();
        return services;
    }
    private static CanonicalModelItem Call(string id, string arguments = "{}") => new(JsonSerializer.SerializeToElement(new { type = "function_call", call_id = id, name = "action", arguments }));
    private static CanonicalModelItem Message(string text, int cost = 1) => new(JsonSerializer.SerializeToElement(new { type = "message", role = "user", content = text, cost }));
    private static CanonicalModelItem Item(string json) { using JsonDocument document = JsonDocument.Parse(json); return new(document.RootElement); }
    private static string? Type(CanonicalModelItem item) => item.Content.TryGetProperty("type", out JsonElement type) ? type.GetString() : null;
    private static ServiceResult<ToolOutput> Success() => ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { result = "confirmed" })));

    private class Probe
    {
        public Clock Clock { get; } = new();
        public ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        public DialogSnapshot Dialog { get; private set; }
        public ModelAccess Access { get; } = new("isolated-key");
        public Queue<ModelResponse> Responses { get; } = new();
        public List<ModelRequest> Requests { get; } = [];
        public List<string> Events { get; } = [];
        public List<Guid> Scopes { get; } = [];
        public int ProviderCalls, AccessCalls, SettingsCalls, Generations, Actions, Compacts, Counts, Reads;
        public int Threshold = 900;
        public string? RejectWrite, ThrowWrite;
        public Exception WriteException = new IOException("unknown write");
        public Exception? GenerateFailure;
        public Exception? ReadException;
        public string? ThrowScopeAt;
        public Exception ScopeException = new IOException("scope cleanup");
        public bool Deleted;
        public bool LargeToolOutputBudget;
        public Action<string>? AfterWrite;
        public Func<ToolInvocation, CancellationToken, Task<ServiceResult<ToolOutput>>> Action = (_, _) => Task.FromResult(Success());
        public Func<Task<ServiceResult<ModelResponse>>> Compact = () => Task.FromResult(ServiceResult<ModelResponse>.Ok(ModelResponse.Completed([Message("small")])));
        public Probe() => Dialog = new(new(Call.DialogId, Guid.NewGuid(), 0), Call.OwnerId, NOW, NOW.AddDays(1), 0, [], null);
        public void Replace(IEnumerable<StoredDialogTurn> turns, DialogWriteToken? token = null, StoredDialogContext? active = null) =>
            Dialog = new(token ?? Dialog.Token, Dialog.OwnerId, Dialog.CreatedAtUtc, Dialog.ExpiresAtUtc, 0, turns, active ?? Dialog.ActiveContext);
    }
    private class Clock : TimeProvider
    {
        public DateTimeOffset Now = NOW;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    /// <inheritdoc/>
    private class Store(Probe probe) : IDialogReader, IDialogTurnWriter, IDialogToolAttemptWriter, IDialogContextWriter, IAsyncDisposable
    {
        private readonly Guid _scope = Guid.NewGuid();
        private string? _lastKind;
        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
        { _lastKind = "read"; probe.Reads++; if (probe.ReadException is not null) throw probe.ReadException; return Task.FromResult(ServiceResult<DialogSnapshot>.Ok(probe.Dialog)); }
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> BeginAsync(DialogAccess access, DialogWriteToken expected, Guid turnId, IReadOnlyList<CanonicalModelItem> input, CancellationToken cancellationToken = default) =>
            Write("begin", access, expected, token => probe.Replace(probe.Dialog.Turns.Append(new(turnId, probe.Dialog.Turns.Count + 1, DialogTurnStatus.InProgress, input, [])), token));
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> AppendAsync(DialogAccess access, DialogWriteToken expected, Guid turnId, IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps, CancellationToken cancellationToken = default) =>
            Write("append", access, expected, token => Change(turnId, token, items, modelSteps));
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> FinishAsync(DialogAccess access, DialogWriteToken expected, Guid turnId, DialogTurnStatus status, IReadOnlyList<CanonicalModelItem> newItems, IReadOnlyList<StoredModelStep> modelSteps, CancellationToken cancellationToken = default) =>
            Write("finish", access, expected, token => Change(turnId, token, newItems, modelSteps, status));
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> StartAsync(DialogAccess access, DialogWriteToken expected, ToolExecutionIdentity identity, CancellationToken cancellationToken = default) =>
            Write("start", access, expected, token => Attempt(identity.Call.TurnId, identity.StepId, new(identity.OutputIndex, identity.Call.AgentId, ToolAttemptState.Started), token));
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogAccess access, DialogWriteToken expected, Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default) =>
            Write("outcomes", access, expected, token =>
            {
                Assert.All(batch.Results, result => Assert.Equal(stepId, result.Identity.StepId));
                Change(turnId, token, batch.Outputs, []);
                foreach (ToolExecutionResult result in batch.Results) Attempt(turnId, stepId, new(result.Identity.OutputIndex, result.Identity.Call.AgentId,
                    result.Status switch { ToolExecutionStatus.Succeeded => ToolAttemptState.Succeeded, ToolExecutionStatus.Rejected => ToolAttemptState.Rejected,
                        ToolExecutionStatus.Unknown => ToolAttemptState.Unknown, _ => ToolAttemptState.NotStarted }), token);
            });
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected, long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default) =>
            Write("context", access, expected, token => probe.Replace(probe.Dialog.Turns, token, new((probe.Dialog.ActiveContext?.Version ?? 0) + 1, throughTurnSequence, compaction)));
        private Task<ServiceResult<DialogWriteToken>> Write(string kind, DialogAccess access, DialogWriteToken expected, Action<DialogWriteToken> apply)
        {
            _lastKind = kind;
            probe.Events.Add(kind); probe.Scopes.Add(_scope);
            if (kind == probe.ThrowWrite) throw probe.WriteException;
            ServiceErrorType? error = probe.Deleted ? ServiceErrorType.NotFound : access.NowUtc >= probe.Dialog.ExpiresAtUtc ? ServiceErrorType.Expired :
                expected.Revision != probe.Dialog.Token.Revision || kind == probe.RejectWrite ? ServiceErrorType.Conflict : null;
            if (error is not null) return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new(error.Value, "write refused")));
            DialogWriteToken next = new(expected.DialogId, expected.IncarnationId, expected.Revision + 1);
            apply(next);
            probe.AfterWrite?.Invoke(kind);
            return Task.FromResult(ServiceResult<DialogWriteToken>.Ok(next));
        }
        /// <inheritdoc/>
        public ValueTask DisposeAsync() => probe.ThrowScopeAt is not null && probe.ThrowScopeAt == _lastKind
            ? ValueTask.FromException(probe.ScopeException) : ValueTask.CompletedTask;
        private void Change(Guid turnId, DialogWriteToken token, IEnumerable<CanonicalModelItem> items, IEnumerable<StoredModelStep> steps, DialogTurnStatus? status = null) =>
            probe.Replace(probe.Dialog.Turns.Select(turn => turn.Id != turnId ? turn : new StoredDialogTurn(turn.Id, turn.Sequence, status ?? turn.Status, turn.Items.Concat(items), turn.ModelSteps.Concat(steps))), token);
        private void Attempt(Guid turnId, Guid stepId, StoredToolAttempt attempt, DialogWriteToken token) =>
            probe.Replace(probe.Dialog.Turns.Select(turn => turn.Id != turnId ? turn : new StoredDialogTurn(turn.Id, turn.Sequence, turn.Status, turn.Items,
                turn.ModelSteps.Select(step => step.StepId != stepId ? step : new StoredModelStep(step.StepId, step.Response, step.ToolAttempts.Where(prior => prior.OutputIndex != attempt.OutputIndex).Append(attempt))))), token);
    }
    /// <inheritdoc/>
    private class Settings(Probe probe) : IModelSettingsReader
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken ct = default) => throw new InvalidOperationException("Unpinned reader запрещён в run.");
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access, string? model = null, string? effort = null, CancellationToken ct = default)
        {
            probe.SettingsCalls++; Assert.Same(probe.Access, access);
            ModelCapabilities capabilities = new("gpt-5", true, 1000, 1000, 100, ["high"], "high", ["text"], true, true, true, true, false);
            return Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Ok(new(capabilities, "high", probe.Threshold, 10)));
        }
    }
    /// <inheritdoc/>
    private class LegacySettings : IModelSettingsReader
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken ct = default) =>
            throw new InvalidOperationException("Fallback на legacy reader запрещён.");
    }
    /// <inheritdoc/>
    private class Access(Probe probe) : IModelAccessResolver
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        { probe.AccessCalls++; return Task.FromResult(ServiceResult<ModelAccess>.Ok(probe.Access)); }
    }
    /// <inheritdoc/>
    private class Gateway(Probe probe) : IModelGateway
    {
        /// <inheritdoc/>
        public async Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        {
            Assert.Same(probe.Access, access); probe.Generations++; probe.Requests.Add(request);
            if (onUpdate is not null) await onUpdate(new("delta", Message("stream")), cancellationToken);
            if (probe.GenerateFailure is not null) throw probe.GenerateFailure;
            return ServiceResult<ModelResponse>.Ok(probe.Responses.Dequeue());
        }
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, CancellationToken cancellationToken = default)
        { Assert.Same(probe.Access, access); probe.Compacts++; return probe.Compact(); }
    }
    /// <inheritdoc/>
    private class Counter(Probe probe) : IContextTokenCounter
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            probe.Counts++;
            long count = request.Input.Sum(item => item.Content.TryGetProperty("cost", out JsonElement cost) ? cost.GetInt32() : 1);
            if (probe.LargeToolOutputBudget && request.Input.Any(item => Type(item) == "function_call_output")) count = 1001;
            bool opaque = request.Input.Any(item => item.Content.TryGetProperty("encrypted_content", out _));
            return Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new("isolated", count, opaque ? null : count, opaque)));
        }
    }
    /// <inheritdoc/>
    private class Provider(Probe probe) : IContextProvider
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default)
        { probe.ProviderCalls++; Assert.Single(request.NewInput); return Task.FromResult(ServiceResult<ContextContribution>.Ok(new([Item("{\"type\":\"message\",\"role\":\"developer\",\"provider\":true}")]))); }
    }
    /// <inheritdoc/>
    private class Validator : IToolInvocationValidator
    {
        /// <inheritdoc/>
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation, CancellationToken cancellationToken = default) =>
            Task.FromResult(invocation.Arguments.ValueKind == JsonValueKind.Object ? ServiceResult.Ok() : ServiceResult.Fail(new(ServiceErrorType.Validation, "object required")));
    }
    /// <inheritdoc/>
    private class Handler(Probe probe) : IToolHandler
    {
        /// <inheritdoc/>
        public ModelToolDefinition Definition => TOOL;
        /// <inheritdoc/>
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref probe.Actions);
            StoredModelStep step = probe.Dialog.Turns.Single(turn => turn.Id == invocation.Call.TurnId).ModelSteps.Last();
            Assert.Contains(step.ToolAttempts, attempt => attempt.State == ToolAttemptState.Started);
            return probe.Action(invocation, cancellationToken);
        }
    }
}
