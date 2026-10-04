using System.Collections.Concurrent;
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

/// <summary>Проверяет public DI/registry/executor и actual ContextBuilder без HTTP, БД или приложения.</summary>
public class ToolExecutorTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly ModelToolDefinition DEFINITION = new("GetOrderStatus", "Статус доступного заказа",
        Json("""{"type":"object","properties":{"orderId":{"type":"string"}},"required":["orderId"],"additionalProperties":false}"""), true);

    /// <summary>Полные schema/описания доступны без создания handler; duplicate exact names отклоняются.</summary>
    [Fact]
    public async Task RegistryPreservesDefinitionRejectsDuplicatesAndDoesNotInstantiateHandlers()
    {
        Probe probe = new();
        ServiceCollection services = Services(probe);
        Assert.Throws<ArgumentException>(() => services.AddAgentBridgeTool<Handler, Validator>(DEFINITION));
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IToolRegistry registry = provider.GetRequiredService<IToolRegistry>();
        Assert.Equal(DEFINITION.Parameters.GetRawText(), Assert.Single(registry.Definitions).Parameters.GetRawText());
        Assert.Equal(DEFINITION.Description, registry.Definitions[0].Description);
        Assert.Equal(0, probe.Created);
        Assert.Null(registry.OpenScope("getorderstatus"));
        Assert.Throws<ArgumentException>(() => new ToolRegistry(
            [new(DEFINITION, typeof(Handler), typeof(Validator)), new(DEFINITION, typeof(Handler), typeof(Validator))],
            provider.GetRequiredService<IServiceScopeFactory>()));
    }

    /// <summary>Owner-authorized result становится canonical парой и участвует в следующем вопросе actual builder.</summary>
    [Fact]
    public async Task OrderResultFlowsIntoNextQuestionAndOtherOwnerIsRejected()
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        CanonicalModelItem call = Function("call-one");
        CanonicalModelItem opaque = Item("""{"type":"reasoning","encrypted_content":"preserved","unknown":[1,null]}""");
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([opaque, call]));
        ToolExecutionBatch batch = (await executor.ExecuteAsync(session, step)).Data!;
        ToolExecutionResult result = Assert.Single(batch.Results);
        Assert.True(batch.CanContinue);
        Assert.Equal(ToolExecutionStatus.Succeeded, result.Status);
        Assert.Equal(1, result.Identity.OutputIndex);
        Assert.Same(session.Call, result.Identity.Call);
        Assert.Equal(session.IncarnationId, result.Identity.IncarnationId);
        Assert.Equal(step.StepId, result.Identity.StepId);
        Assert.Equal("call-one", result.Output!.Content.GetProperty("call_id").GetString());
        Assert.Equal("function_call_output", result.Output.Content.GetProperty("type").GetString());
        using JsonDocument output = JsonDocument.Parse(result.Output.Content.GetProperty("output").GetString()!);
        Assert.Equal("order-a", output.RootElement.GetProperty("orderId").GetString());
        Assert.Equal("owner-a", output.RootElement.GetProperty("owner").GetString());
        Assert.True(output.RootElement.GetProperty("future").GetProperty("kept").GetBoolean());
        Assert.Equal(1, probe.Started);
        Assert.Equal(1, probe.Disposed);

        // Имитируется уже прочитанная canonical history, а не atomic persistence.
        StoredDialogTurn turn = new(session.Call.TurnId, 1, DialogTurnStatus.Completed,
            [opaque, call, result.Output], [step]);
        DialogSnapshot snapshot = new(new(session.Call.DialogId, session.IncarnationId, 2), session.Call.OwnerId, NOW, NOW.AddDays(1),
            0, [turn], null);
        ModelRequest next = new("gpt-5", "high", "instructions", [Item("""{"type":"message","role":"user","content":"Когда доставят?"}""")], [DEFINITION]);
        ContextBuilder builder = new([]);
        ServiceResult<ModelRequest> prepared = await builder.BuildAsync(session.Call, snapshot, next, NOW);
        Assert.True(prepared.Success);
        Assert.Equal([opaque, call, result.Output, next.Input[0]], prepared.Data!.Input);
        ApplicationCallContext other = new(session.Call.DialogId, DialogOwnerId.From("owner-b"), Guid.NewGuid(), "agent");
        ServiceResult<ModelRequest> forbidden = await builder.BuildAsync(other, snapshot, next, NOW);
        Assert.False(forbidden.Success);
        Assert.Equal(ServiceErrorType.Forbidden, forbidden.Error!.Type);
        Assert.Equal(opaque.Content.GetRawText(), prepared.Data.Input[0].Content.GetRawText());
    }

    /// <summary>Все pre-action отказы не запускают handler и не раскрывают сообщения validator.</summary>
    [Theory]
    [InlineData("unknown", "unknown", "order-a", ServiceErrorType.NotFound)]
    [InlineData("GetOrderStatus", "other", "order-a", ServiceErrorType.Forbidden)]
    [InlineData("GetOrderStatus", "GetOrderStatus", "order-b", ServiceErrorType.Forbidden)]
    [InlineData("GetOrderStatus", "GetOrderStatus", "", ServiceErrorType.Validation)]
    public async Task UnknownForbiddenAndSchemaInvalidProduceExplicitSafeError(string name, string selected,
        string orderId, ServiceErrorType expected)
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor, selected: [selected]);
        ToolExecutionBatch batch = (await executor.ExecuteAsync(session, Step(Function("call", name, orderId)))).Data!;
        ToolExecutionResult result = Assert.Single(batch.Results);
        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        Assert.Equal(expected, result.Error!.Type);
        Assert.True(batch.CanContinue);
        Assert.Equal(0, probe.Started);
        Assert.DoesNotContain("secret", result.Output!.Content.GetRawText());
        using JsonDocument error = JsonDocument.Parse(result.Output.Content.GetProperty("output").GetString()!);
        Assert.Equal(expected.ToString(), error.RootElement.GetProperty("error").GetProperty("type").GetString());
    }

    /// <summary>App validator проверяет required/type/additionalProperties полной зарегистрированной схемы до действия.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"orderId":1}""")]
    [InlineData("""{"orderId":"order-a","admin":true}""")]
    public async Task ApplicationSchemaValidatorRejectsMissingWrongTypeAndExtraProperties(string arguments)
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        CanonicalModelItem call = new(JsonSerializer.SerializeToElement(new { type = "function_call", call_id = "call",
            name = DEFINITION.Name, arguments }));
        ToolExecutionBatch batch = (await executor.ExecuteAsync(Session(executor), Step(call))).Data!;
        Assert.Equal(ServiceErrorType.Validation, batch.Results[0].Error!.Type);
        Assert.Equal(0, probe.Started);
        Assert.Equal(1, probe.Validations);
    }

    /// <summary>Весь malformed шаг проверяется до любых действий, включая partial arguments и orphan output.</summary>
    [Theory]
    [InlineData("""{"type":"function_call","call_id":"c","name":"GetOrderStatus","arguments":"{"}""")]
    [InlineData("""{"type":"function_call","call_id":"c","name":"GetOrderStatus","arguments":"[]"}""")]
    [InlineData("""{"type":"function_call","call_id":" ","name":"GetOrderStatus","arguments":"{}"}""")]
    [InlineData("""{"type":"function_call","call_id":"c","name":" ","arguments":"{}"}""")]
    [InlineData("""{"type":"function_call","call_id":"c","name":"GetOrderStatus","arguments":{}}""")]
    [InlineData("""{"type":"function_call","call_id":"c","name":"GetOrderStatus","arguments":"{}","status":"in_progress"}""")]
    [InlineData("""{"type":"function_call_output","call_id":"orphan","output":"{}"}""")]
    public async Task MalformedWholeStepHasNoSideEffects(string malformed)
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ServiceResult<ToolExecutionBatch> result = await executor.ExecuteAsync(Session(executor), Step(Function("good"), Item(malformed)));
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal(0, probe.Started);
        Assert.Equal(0, probe.Created);
    }

    /// <summary>Неподтверждённый lifecycle запрещает исполнение даже с полными arguments.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Canceled)]
    [InlineData(ModelResponseStatus.Failed)]
    public async Task PartialResponsesDoNotExecuteTools(ModelResponseStatus status)
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ModelResponse response = status switch
        {
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete([Function("call")]),
            ModelResponseStatus.Canceled => ModelResponse.Canceled([Function("call")]),
            _ => ModelResponse.Failed([Function("call")], new(ServiceErrorType.Rejected, "safe"))
        };
        Assert.False((await executor.ExecuteAsync(Session(executor), new(Guid.NewGuid(), response))).Success);
        Assert.Equal(0, probe.Started);
    }

    /// <summary>Repeated call_id разрешён в разных парах/шагах; повтор того же step запрещён.</summary>
    [Fact]
    public async Task RepeatedCallIdsAreNotGlobalExecutionKeys()
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        StoredModelStep first = Step(Function("same"));
        ToolExecutionBatch firstBatch = (await executor.ExecuteAsync(session, first)).Data!;
        ServiceResult<ToolExecutionBatch> repeated = await executor.ExecuteAsync(session, first);
        Assert.Equal(ServiceErrorType.Conflict, repeated.Error!.Type);
        StoredModelStep second = Step(Function("same"));
        ToolExecutionBatch secondBatch = (await executor.ExecuteAsync(session, second)).Data!;
        Assert.NotEqual(firstBatch.Results[0].Identity.StepId, secondBatch.Results[0].Identity.StepId);
        Assert.Equal(2, probe.Started);
        StoredModelStep closed = Step(Function("same"), firstBatch.Outputs[0], Function("same"), secondBatch.Outputs[0], Function("same"));
        ToolExecutionBatch third = (await executor.ExecuteAsync(session, closed)).Data!;
        Assert.Single(third.Results);
        Assert.Equal(4, third.Results[0].Identity.OutputIndex);
        Assert.Equal(3, probe.Started);
        ToolExecutionBatch overlapping = (await executor.ExecuteAsync(session, Step(Function("overlap"), Function("overlap")))).Data!;
        Assert.Equal([0, 1], overlapping.Results.Select(result => result.Identity.OutputIndex));
        Assert.Equal(5, probe.Started);
        ToolExecutionBatch alreadyClosed = (await executor.ExecuteAsync(session, Step(Function("overlap"), Function("overlap"),
            overlapping.Outputs[0], overlapping.Outputs[1]))).Data!;
        Assert.Empty(alreadyClosed.Results);
        Assert.Equal(5, probe.Started);
    }

    /// <summary>Ограничение шагов и calls применяется до действия; expiry фиксирован и не продлевается.</summary>
    [Fact]
    public async Task BoundsAndFixedExpiryRejectBeforeAction()
    {
        Probe probe = new();
        ManualClock clock = new();
        await using ServiceProvider provider = Services(probe, clock).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor, new(1, 1, 1, TimeSpan.FromMinutes(1)));
        Assert.False((await executor.ExecuteAsync(session, Step(Function("one"), Function("two")))).Success);
        Assert.Equal(0, probe.Started);
        Assert.True((await executor.ExecuteAsync(session, Step(Function("one")))).Data!.CanContinue);
        Assert.Equal(ServiceErrorType.Rejected, (await executor.ExecuteAsync(session, Step(Function("two")))).Error!.Type);
        ToolExecutionSession expired = Session(executor);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(ServiceErrorType.Expired, (await executor.ExecuteAsync(expired, Step(Function("expired")))).Error!.Type);
        Assert.Equal(NOW.AddDays(1), expired.ExpiresAtUtc);
        Assert.Equal(1, probe.Started);
    }

    /// <summary>Общий monotonic timeout включает промежутки между шагами.</summary>
    [Fact]
    public async Task TotalTimeoutDoesNotResetBetweenSteps()
    {
        ManualClock clock = new();
        Probe probe = new();
        await using ServiceProvider provider = Services(probe, clock).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor, new(8, 16, 1, TimeSpan.FromSeconds(1)));
        Assert.True((await executor.ExecuteAsync(session, Step(Function("one")))).Success);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(ServiceErrorType.Timeout, (await executor.ExecuteAsync(session, Step(Function("two")))).Error!.Type);
        Assert.Equal(1, probe.Started);
    }

    /// <summary>Подтверждённый failure не отменяет соседний успех и не становится выдуманным успехом.</summary>
    [Fact]
    public async Task ConfirmedFailurePreservesOtherOutputAndScrubsRawError()
    {
        Probe probe = new();
        probe.Run = (invocation, _, _) => Task.FromResult(invocation.CallId == "fail"
            ? ServiceResult<ToolOutput>.Fail(new(ServiceErrorType.Rejected, "secret raw business data")) : Success(invocation));
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionBatch batch = (await executor.ExecuteAsync(Session(executor), Step(Function("ok"), Function("fail")))).Data!;
        Assert.Equal([ToolExecutionStatus.Succeeded, ToolExecutionStatus.Rejected], batch.Results.Select(result => result.Status));
        Assert.Equal(2, batch.Outputs.Count);
        Assert.True(batch.CanContinue);
        Assert.DoesNotContain("secret", batch.Results[1].Output!.Content.GetRawText());
        Assert.Equal(2, probe.Disposed);
    }

    /// <summary>Неожиданная ошибка сохраняет предыдущий успех/Unknown, распространяется и блокирует повтор.</summary>
    [Fact]
    public async Task UnexpectedFailurePreservesPartialReportAndStopsSession()
    {
        InvalidOperationException original = new("secret exception");
        Probe probe = new();
        probe.Run = (invocation, _, _) => invocation.CallId == "fail" ? Task.FromException<ServiceResult<ToolOutput>>(original)
            : Task.FromResult(Success(invocation));
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        StoredModelStep step = Step(Function("ok"), Function("fail"), Function("unstarted"));
        Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(session, step)));
        ToolExecutionBatch report = session.LastResult!;
        Assert.Equal([ToolExecutionStatus.Succeeded, ToolExecutionStatus.Unknown, ToolExecutionStatus.NotStarted],
            report.Results.Select(result => result.Status));
        Assert.Single(report.Outputs);
        Assert.Null(report.Results[1].Output);
        Assert.DoesNotContain("secret", report.Results[1].Error!.Message);
        Assert.Equal(ServiceErrorType.Conflict, (await executor.ExecuteAsync(session, step)).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await executor.ExecuteAsync(session, Step(Function("other")))).Error!.Type);
        Assert.Equal(2, probe.Started);
        Assert.Equal(2, probe.Disposed);
    }

    /// <summary>Timeout failure handler имеет неизвестный исход и не порождает ложный output.</summary>
    [Fact]
    public async Task HandlerTimeoutIsUnknownAndNotAutomaticallyRepeated()
    {
        Probe probe = new();
        probe.Run = (_, _, _) => Task.FromResult(ServiceResult<ToolOutput>.Fail(new(ServiceErrorType.Timeout, "secret")));
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        ToolExecutionBatch report = (await executor.ExecuteAsync(session, Step(Function("timeout"), Function("later")))).Data!;
        Assert.Equal(ToolExecutionStatus.Unknown, report.Results[0].Status);
        Assert.Empty(report.Outputs);
        Assert.False(report.CanContinue);
        Assert.False((await executor.ExecuteAsync(session, Step(Function("retry")))).Success);
        Assert.Equal(1, probe.Started);
    }

    /// <summary>Caller cancellation ждёт освобождение scope и сохраняет original caller token и Unknown.</summary>
    [Fact]
    public async Task CallerCancellationWaitsForHandlerAndRetainsUnknown()
    {
        Probe probe = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Run = async (_, _, token) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); throw new InvalidOperationException(); };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        using CancellationTokenSource cancellation = new();
        Task<ServiceResult<ToolExecutionBatch>> execution = executor.ExecuteAsync(session, Step(Function("cancel")), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ServiceErrorType.Conflict, (await executor.ExecuteAsync(session, Step(Function("concurrent")))).Error!.Type);
            cancellation.Cancel();
            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => execution);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.Equal(ToolExecutionStatus.Unknown, session.LastResult!.Results[0].Status);
            Assert.Empty(session.LastResult.Outputs);
            Assert.Equal(1, probe.Disposed);
        }
        finally
        {
            cancellation.Cancel();
            try { await execution; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Unrelated OCE является неожиданной ошибкой, а не успешным deadline report.</summary>
    [Fact]
    public async Task UnrelatedCancellationExceptionPropagates()
    {
        OperationCanceledException original = new("unrelated");
        Probe probe = new() { Run = (_, _, _) => Task.FromException<ServiceResult<ToolOutput>>(original) };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        Assert.Same(original, await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(session, Step(Function("call")))));
        Assert.Equal(ToolExecutionStatus.Unknown, session.LastResult!.Results[0].Status);
    }

    /// <summary>Parallel invocation tasks имеют разные scoped state; validator/handler одного вызова разделяют только свой scope.</summary>
    [Fact]
    public async Task ParallelTasksHaveSeparateScopedStateAndBoundedConcurrency()
    {
        Probe probe = new();
        TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0;
        int maximum = 0;
        ConcurrentDictionary<Guid, bool> scopes = new();
        probe.Run = async (invocation, state, token) =>
        {
            Assert.True(state.Validated);
            Assert.True(scopes.TryAdd(state.Id, true));
            int count = Interlocked.Increment(ref active);
            Interlocked.Exchange(ref maximum, Math.Max(maximum, count));
            if (scopes.Count == 2) bothEntered.TrySetResult();
            try { await release.Task.WaitAsync(token); return Success(invocation); }
            finally { Interlocked.Decrement(ref active); }
        };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        using CancellationTokenSource cleanup = new();
        Task<ServiceResult<ToolExecutionBatch>> execution = executor.ExecuteAsync(
            Session(executor, new(8, 16, 2, TimeSpan.FromMinutes(1))), Step(Function("a"), Function("b"), Function("c")), cleanup.Token);
        try
        {
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, probe.Started);
            release.SetResult();
            ToolExecutionBatch batch = (await execution).Data!;
            Assert.True(batch.CanContinue);
            Assert.Equal(["a", "b", "c"], batch.Results.Select(result => result.Invocation.CallId));
            Assert.Equal(3, scopes.Count);
            Assert.InRange(maximum, 2, 2);
            Assert.Equal(0, active);
            Assert.Equal(3, probe.Disposed);
        }
        finally
        {
            release.TrySetResult();
            cleanup.Cancel();
            try { await execution; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Одновременно исполняемые сессии передают только своего владельца и своё состояние.</summary>
    [Fact]
    public async Task OwnersAreIsolatedAcrossSessions()
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        Task<ServiceResult<ToolExecutionBatch>> first = executor.ExecuteAsync(Session(executor), Step(Function("same")));
        Task<ServiceResult<ToolExecutionBatch>> second = executor.ExecuteAsync(Session(executor, owner: "owner-b"),
            Step(Function("same", orderId: "order-b")));
        ServiceResult<ToolExecutionBatch>[] batches = await Task.WhenAll(first, second);
        Assert.Equal(["owner-a", "owner-b"], batches.Select(batch => batch.Data!.Results[0].Invocation.Call.OwnerId.Value));
        Assert.DoesNotContain("owner-b", batches[0].Data!.Outputs[0].Content.GetRawText());
        Assert.DoesNotContain("owner-a", batches[1].Data!.Outputs[0].Content.GetRawText());
    }

    /// <summary>Schema mismatch между регистрацией и handler выявляется до validator/action; scope всё равно освобождается.</summary>
    [Fact]
    public async Task HandlerDefinitionMismatchFailsFastAndDisposesScope()
    {
        Probe probe = new() { Definition = new("GetOrderStatus", "другое описание", DEFINITION.Parameters, true) };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(session, Step(Function("call"))));
        Assert.Equal(0, probe.Validations);
        Assert.Equal(0, probe.Started);
        Assert.Equal(1, probe.Disposed);
        Assert.Equal(ToolExecutionStatus.NotStarted, session.LastResult!.Results[0].Status);
    }

    /// <summary>Первичная ошибка и ошибка cleanup сохраняются вместе; успешный сосед не теряется.</summary>
    [Fact]
    public async Task PrimaryAndDisposeFailuresAreBothPreserved()
    {
        InvalidOperationException primary = new("primary secret");
        InvalidOperationException cleanup = new("cleanup secret");
        Probe probe = new();
        probe.Run = (invocation, _, _) =>
        {
            if (invocation.CallId != "fail") return Task.FromResult(Success(invocation));
            probe.DisposeError = cleanup;
            return Task.FromException<ServiceResult<ToolOutput>>(primary);
        };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() => executor.ExecuteAsync(session,
            Step(Function("ok"), Function("fail"))));
        Assert.Equal([primary, cleanup], exception.InnerExceptions);
        Assert.Single(session.LastResult!.Outputs);
        Assert.Equal(ToolExecutionStatus.Unknown, session.LastResult.Results[1].Status);
        Assert.Equal(2, probe.Disposed);
    }

    /// <summary>Cleanup failure после достоверного результата сохраняет output, но останавливает сессию.</summary>
    [Fact]
    public async Task DisposeFailureKeepsConfirmedSuccess()
    {
        InvalidOperationException cleanup = new("cleanup secret");
        Probe probe = new() { DisposeError = cleanup };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        Assert.Same(cleanup, await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(session, Step(Function("ok")))));
        Assert.Equal(ToolExecutionStatus.Succeeded, session.LastResult!.Results[0].Status);
        Assert.Single(session.LastResult.Outputs);
        Assert.False(session.LastResult.CanContinue);
    }

    /// <summary>Поздняя caller cancellation не удаляет подтверждённый output и запрещает следующий step.</summary>
    [Fact]
    public async Task LateCallerCancellationKeepsOutputAndStopsSession()
    {
        using CancellationTokenSource caller = new();
        Probe probe = new();
        probe.Run = (invocation, _, _) => { caller.Cancel(); return Task.FromResult(Success(invocation)); };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(session, Step(Function("ok")), caller.Token));
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Equal(ToolExecutionStatus.Succeeded, session.LastResult!.Results[0].Status);
        Assert.Single(session.LastResult.Outputs);
        Assert.False(session.LastResult.CanContinue);
        Assert.Equal(ServiceErrorType.Conflict, (await executor.ExecuteAsync(session, Step(Function("next")))).Error!.Type);
        Assert.Equal(1, probe.Started);
    }

    /// <summary>Детерминированный deadline отменяет начатый handler, ожидает cleanup и сохраняет Unknown.</summary>
    [Fact]
    public async Task DeadlineCancelsStartedHandlerAndWaitsForCleanup()
    {
        ManualClock clock = new();
        Probe probe = new();
        probe.Run = async (invocation, _, token) =>
        {
            if (invocation.CallId == "ok") return Success(invocation);
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        };
        await using ServiceProvider provider = Services(probe, clock).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor, new(8, 16, 1, TimeSpan.FromSeconds(1)));
        ToolExecutionBatch batch = (await executor.ExecuteAsync(session, Step(Function("ok"), Function("deadline"), Function("later")))).Data!;
        Assert.Equal(ServiceErrorType.Timeout, batch.Error!.Type);
        Assert.Equal([ToolExecutionStatus.Succeeded, ToolExecutionStatus.Unknown, ToolExecutionStatus.NotStarted],
            batch.Results.Select(result => result.Status));
        Assert.Single(batch.Outputs);
        Assert.Equal(2, probe.Disposed);
        Assert.False((await executor.ExecuteAsync(session, Step(Function("retry")))).Success);
    }

    /// <summary>Expiry между validator и действием не разрешает начать handler.</summary>
    [Fact]
    public async Task ExpiryAfterAuthorizationDoesNotStartAction()
    {
        ManualClock clock = new();
        Probe probe = new() { AfterValidation = () => clock.Advance(TimeSpan.FromDays(1)) };
        await using ServiceProvider provider = Services(probe, clock).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor);
        ToolExecutionBatch batch = (await executor.ExecuteAsync(session, Step(Function("late")))).Data!;
        Assert.Equal(ServiceErrorType.Expired, batch.Error!.Type);
        Assert.Equal(ToolExecutionStatus.NotStarted, batch.Results[0].Status);
        Assert.Empty(batch.Outputs);
        Assert.Equal(0, probe.Started);
        Assert.Equal(1, probe.Disposed);
    }

    /// <summary>Не соблюдающий cancellation handler всё равно ожидается; подтверждённый поздний результат не теряется.</summary>
    [Fact]
    public async Task UncooperativeHandlerIsAwaitedWithoutClaimingHardDeadline()
    {
        ManualClock clock = new();
        Probe probe = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Run = async (invocation, _, _) => { entered.SetResult(); await release.Task; return Success(invocation); };
        await using ServiceProvider provider = Services(probe, clock).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor, new(8, 16, 1, TimeSpan.FromSeconds(1)));
        Task<ServiceResult<ToolExecutionBatch>> execution = executor.ExecuteAsync(session, Step(Function("late")));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, probe.Disposed);
            release.SetResult();
            ToolExecutionBatch batch = (await execution).Data!;
            Assert.Equal(ServiceErrorType.Timeout, batch.Error!.Type);
            Assert.Equal(ToolExecutionStatus.Succeeded, batch.Results[0].Status);
            Assert.Single(batch.Outputs);
            Assert.Equal(1, probe.Disposed);
        }
        finally { release.TrySetResult(); await execution; }
    }

    /// <summary>Checkpoint awaited после authorizer, до handler, получает stable identity и может запретить действие.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckpointIsAwaitedBeforeActionAndMayBlockRecovery(bool allow)
    {
        Probe probe = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ToolExecutionIdentity? received = null;
        Checkpoint checkpoint = new(async (identity, invocation, token) =>
        {
            Assert.Equal(1, probe.Validations);
            Assert.Equal(0, probe.Started);
            Assert.Equal("owner-a", invocation.Call.OwnerId.Value);
            received = identity;
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return allow ? ServiceResult.Ok() : ServiceResult.Fail(new(ServiceErrorType.Conflict, "secret recovery state"));
        });
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession baseline = Session(executor);
        ToolExecutionSession session = executor.CreateSession(baseline.Call,
            new(baseline.Call.DialogId, baseline.IncarnationId, 0), baseline.ExpiresAtUtc, [DEFINITION.Name], baseline.Limits, checkpoint);
        StoredModelStep step = Step(Function("call"));
        using CancellationTokenSource cleanup = new();
        Task<ServiceResult<ToolExecutionBatch>> execution = executor.ExecuteAsync(session, step, cleanup.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, probe.Started);
            release.SetResult();
            ToolExecutionBatch batch = (await execution).Data!;
            Assert.Same(received, batch.Results[0].Identity);
            Assert.Equal(step.StepId, received!.StepId);
            Assert.Equal(session.IncarnationId, received.IncarnationId);
            Assert.Equal(allow ? 1 : 0, probe.Started);
            Assert.Equal(allow, batch.CanContinue);
            if (!allow)
            {
                Assert.Equal(ToolExecutionStatus.NotStarted, batch.Results[0].Status);
                Assert.Empty(batch.Outputs);
                Assert.False((await executor.ExecuteAsync(session, Step(Function("retry")))).Success);
                Assert.DoesNotContain("secret", batch.Error!.Message);
            }
        }
        finally
        {
            release.TrySetResult();
            cleanup.Cancel();
            try { await execution; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Неправильный dialog token, не-UTC expiry и невалидные limits не создают сессию.</summary>
    [Fact]
    public async Task InvalidSessionParametersFailFast()
    {
        Probe probe = new();
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession valid = Session(executor);
        Assert.Throws<ArgumentException>(() => executor.CreateSession(valid.Call,
            new(DialogId.From(Guid.NewGuid()), Guid.NewGuid(), 0), valid.ExpiresAtUtc, [DEFINITION.Name], valid.Limits));
        Assert.Throws<ArgumentException>(() => executor.CreateSession(valid.Call,
            new(valid.Call.DialogId, valid.IncarnationId, 0), valid.ExpiresAtUtc.ToOffset(TimeSpan.FromHours(1)), [DEFINITION.Name], valid.Limits));
        Assert.Throws<ArgumentException>(() => executor.CreateSession(valid.Call,
            new(valid.Call.DialogId, valid.IncarnationId, 0), valid.ExpiresAtUtc, [DEFINITION.Name, DEFINITION.Name], valid.Limits));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolExecutionLimits(0, 1, 1, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolExecutionLimits(1, 0, 1, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolExecutionLimits(1, 1, 0, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolExecutionLimits(1, 1, 1, TimeSpan.Zero));
        Assert.Equal(0, probe.Started);
    }

    /// <summary>Неизвестный исход записи checkpoint не разрешает handler или повтор; исключение не маскируется.</summary>
    [Fact]
    public async Task CheckpointExceptionNeverStartsOrRetriesHandler()
    {
        InvalidOperationException original = new("unknown save outcome");
        Probe probe = new();
        Checkpoint checkpoint = new((_, _, _) => Task.FromException<ServiceResult>(original));
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession baseline = Session(executor);
        ToolExecutionSession session = executor.CreateSession(baseline.Call,
            new(baseline.Call.DialogId, baseline.IncarnationId, 0), baseline.ExpiresAtUtc, [DEFINITION.Name], baseline.Limits, checkpoint);
        Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(session, Step(Function("call")))));
        Assert.Equal(ToolExecutionStatus.NotStarted, session.LastResult!.Results[0].Status);
        Assert.False(session.LastResult.CanContinue);
        Assert.False((await executor.ExecuteAsync(session, Step(Function("retry")))).Success);
        Assert.Equal(0, probe.Started);
        Assert.Equal(1, probe.Disposed);
    }

    /// <summary>Отмена во время checkpoint ожидается, освобождает scope и не допускает бизнес-действие.</summary>
    [Fact]
    public async Task CheckpointCancellationPreventsActionAndStopsSession()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Probe probe = new();
        Checkpoint checkpoint = new(async (_, _, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return ServiceResult.Ok();
        });
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession baseline = Session(executor);
        ToolExecutionSession session = executor.CreateSession(baseline.Call,
            new(baseline.Call.DialogId, baseline.IncarnationId, 0), baseline.ExpiresAtUtc, [DEFINITION.Name], baseline.Limits, checkpoint);
        using CancellationTokenSource caller = new();
        Task<ServiceResult<ToolExecutionBatch>> execution = executor.ExecuteAsync(session, Step(Function("call")), caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            caller.Cancel();
            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => execution);
            Assert.Equal(caller.Token, exception.CancellationToken);
            Assert.Equal(ToolExecutionStatus.NotStarted, session.LastResult!.Results[0].Status);
            Assert.Empty(session.LastResult.Outputs);
            Assert.False((await executor.ExecuteAsync(session, Step(Function("retry")))).Success);
            Assert.Equal(0, probe.Started);
            Assert.Equal(1, probe.Disposed);
        }
        finally
        {
            caller.Cancel();
            try { await execution; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>При parallel failure все начатые handlers ожидаются; поздний достоверный соседний output сохраняется.</summary>
    [Fact]
    public async Task ParallelFailureWaitsForOtherHandlerBeforePropagating()
    {
        InvalidOperationException original = new("primary");
        TaskCompletionSource slowEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource failureObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSlow = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Probe probe = new();
        probe.Run = async (invocation, _, token) =>
        {
            if (invocation.CallId == "fail")
            {
                await slowEntered.Task.WaitAsync(token);
                failureObserved.SetResult();
                throw original;
            }
            slowEntered.SetResult();
            await releaseSlow.Task;
            return Success(invocation);
        };
        await using ServiceProvider provider = Services(probe).BuildServiceProvider();
        IToolExecutor executor = provider.GetRequiredService<IToolExecutor>();
        ToolExecutionSession session = Session(executor, new(8, 16, 2, TimeSpan.FromMinutes(1)));
        using CancellationTokenSource cleanup = new();
        Task<ServiceResult<ToolExecutionBatch>> execution = executor.ExecuteAsync(session, Step(Function("fail"), Function("slow")), cleanup.Token);
        try
        {
            await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            releaseSlow.SetResult();
            Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => execution));
            Assert.Equal([ToolExecutionStatus.Unknown, ToolExecutionStatus.Succeeded], session.LastResult!.Results.Select(result => result.Status));
            Assert.Single(session.LastResult.Outputs);
            Assert.Equal(2, probe.Disposed);
        }
        finally
        {
            releaseSlow.TrySetResult();
            cleanup.Cancel();
            try { await execution; } catch (InvalidOperationException) { } catch (OperationCanceledException) { }
        }
    }

    private static ServiceCollection Services(Probe probe, TimeProvider? clock = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(probe);
        services.AddSingleton(clock ?? new ManualClock());
        services.AddScoped<ScopedState>();
        services.AddAgentBridgeTool<Handler, Validator>(DEFINITION);
        return services;
    }

    private static ToolExecutionSession Session(IToolExecutor executor, ToolExecutionLimits? limits = null,
        IEnumerable<string>? selected = null, string owner = "owner-a")
    {
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From(owner), Guid.NewGuid(), "agent");
        return executor.CreateSession(call, new(call.DialogId, Guid.NewGuid(), 0), NOW.AddDays(1), selected ?? ["GetOrderStatus"],
            limits ?? new(8, 16, 1, TimeSpan.FromMinutes(1)));
    }

    private static StoredModelStep Step(params CanonicalModelItem[] items) => new(Guid.NewGuid(), ModelResponse.Completed(items));
    private static CanonicalModelItem Function(string id, string name = "GetOrderStatus", string orderId = "order-a") =>
        new(JsonSerializer.SerializeToElement(new { type = "function_call", call_id = id, name,
            arguments = JsonSerializer.Serialize(new { orderId }) }));
    private static CanonicalModelItem Item(string json) => new(Json(json));
    private static JsonElement Json(string json) { using JsonDocument document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
    private static ServiceResult<ToolOutput> Success(ToolInvocation invocation) => ServiceResult<ToolOutput>.Ok(new(
        JsonSerializer.SerializeToElement(new { orderId = invocation.Arguments.GetProperty("orderId").GetString(),
            owner = invocation.Call.OwnerId.Value, status = "В пути", future = new { kept = true } })));

    private class Probe
    {
        public int Created;
        public int Started;
        public int Disposed;
        public int Validations;
        public Exception? DisposeError;
        public Action? AfterValidation;
        public ModelToolDefinition Definition { get; set; } = DEFINITION;
        public Func<ToolInvocation, ScopedState, CancellationToken, Task<ServiceResult<ToolOutput>>> Run { get; set; } =
            (invocation, _, _) => Task.FromResult(Success(invocation));
    }

    private class Checkpoint(Func<ToolExecutionIdentity, ToolInvocation, CancellationToken, Task<ServiceResult>> before) : IToolExecutionCheckpoint
    {
        public Task<ServiceResult> BeforeExecuteAsync(ToolExecutionIdentity identity, ToolInvocation invocation,
            CancellationToken cancellationToken = default) => before(identity, invocation, cancellationToken);
    }

    private class ScopedState(Probe probe) : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Validated { get; set; }
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref probe.Disposed);
            return probe.DisposeError is null ? ValueTask.CompletedTask : ValueTask.FromException(probe.DisposeError);
        }
    }

    private class Handler : IToolHandler
    {
        private readonly Probe _probe;
        private readonly ScopedState _state;
        public Handler(Probe probe, ScopedState state) { _probe = probe; _state = state; Interlocked.Increment(ref probe.Created); }
        public ModelToolDefinition Definition => _probe.Definition;
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            Assert.True(_state.Validated);
            Interlocked.Increment(ref _probe.Started);
            return _probe.Run(invocation, _state, cancellationToken);
        }
    }

    private class Validator(Probe probe, ScopedState state) : IToolInvocationValidator
    {
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref probe.Validations);
            // Настоящая полная схема этого fixture: required string orderId, additionalProperties=false.
            if (invocation.Arguments.EnumerateObject().Count() != 1
                || !invocation.Arguments.TryGetProperty("orderId", out JsonElement order)
                || order.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(order.GetString()))
                return Task.FromResult(ServiceResult.Fail(new(ServiceErrorType.Validation, "secret schema input")));
            string allowedOrder = invocation.Call.OwnerId.Value == "owner-a" ? "order-a" : "order-b";
            if (order.GetString() != allowedOrder)
                return Task.FromResult(ServiceResult.Fail(new(ServiceErrorType.Forbidden, "secret owner data")));
            state.Validated = true;
            probe.AfterValidation?.Invoke();
            return Task.FromResult(ServiceResult.Ok());
        }
    }

    private class ManualClock : TimeProvider
    {
        private TimeSpan _elapsed;
        private readonly List<ManualTimer> _timers = [];
        public override DateTimeOffset GetUtcNow() => NOW + _elapsed;
        public override long GetTimestamp() => _elapsed.Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ManualTimer timer = new(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
        public void Advance(TimeSpan elapsed)
        {
            _elapsed += elapsed;
            foreach (ManualTimer timer in _timers.ToArray()) timer.FireIfDue();
        }

        private class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan? _due;
            private TimeSpan _period;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._elapsed + dueTime;
                _period = period;
                return true;
            }
            public void FireIfDue()
            {
                if (_due is null || _due > clock._elapsed) return;
                _due = _period == Timeout.InfiniteTimeSpan ? null : clock._elapsed + _period;
                callback(state);
            }
            public void Dispose() => _due = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
