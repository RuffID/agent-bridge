using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Публичный DI/scenario с actual builder и управляемыми внешними портами без HTTP/БД.</summary>
public class ContextCompactorTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogId ID = DialogId.From(Guid.NewGuid());
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From("owner");

    /// <summary>Provider, текущий tail, новый input и tools не попадают в окно и не дублируются после save.</summary>
    [Fact]
    public async Task SeparatesPersistablePrefixAndFreezesProviders()
    {
        using Fixture fixture = new();
        StoredDialogContext previous = new(4, 1, ModelResponse.Completed([Text("old-window")]));
        DialogSnapshot snapshot = Snapshot([
            Turn(1, DialogTurnStatus.Completed, [Text("already-covered")]),
            Turn(2, DialogTurnStatus.Failed, [Text(new string('h', 200))]),
            Turn(3, DialogTurnStatus.InProgress, [Text("running")]),
            Turn(4, DialogTurnStatus.Completed, [Text("after-running")])], previous);
        ModelRequest request = new("gpt-5", "medium", "instructions", [Text("new")],
            [new("tool", "description", Json("{}"), false)], parameters: new(Json("""{"text":{"format":{"type":"text"}},"service_tier":"priority"}""")));
        ContextCompactionResult result = (await fixture.Run(snapshot, request, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.TargetReached, result.Status);
        ModelRequest sent = Assert.Single(fixture.Gateway.Requests);
        Assert.Equal(new[] { "old-window", new string('h', 200) }, sent.Input.Select(Content));
        Assert.Empty(sent.Tools);
        Assert.False(sent.Parameters!.Content.TryGetProperty("text", out _));
        Assert.Equal("priority", sent.Parameters.Content.GetProperty("service_tier").GetString());
        Assert.Equal(new[] { "provider", "small", "running", "after-running", "new" }, result.PreparedRequest.Input.Select(Content));
        Assert.Same(request.Tools[0], result.PreparedRequest.Tools[0]);
        Assert.Same(request.Parameters, result.PreparedRequest.Parameters);
        Assert.Equal(request.Instructions, result.PreparedRequest.Instructions);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(2, result.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(5, result.ActiveContext.Version);
        Assert.Same(snapshot.Token, fixture.Writer.Tokens[0]);
        Assert.Same(previous, snapshot.ActiveContext);
        Assert.Equal(4, snapshot.Turns.Count);
        Assert.Equal(NOW.AddHours(1), snapshot.ExpiresAtUtc);
        Assert.Equal(100, snapshot.ContentBytes);
    }

    /// <summary>Встроенный offline counter сохраняет opaque окно и честный UnknownBudget.</summary>
    [Fact]
    public async Task ActualOfflineCounterAcceptsOpaqueThenGuardStillRejects()
    {
        using Fixture fixture = new(actualCounter: true);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed([Item("""{"type":"compaction","encrypted_content":"opaque","future":{"x":1}}""")], new(Json("""{"object":"response.compaction","output":[],"usage":{"input_tokens":1}}"""))));
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, [Text(string.Concat(Enumerable.Repeat("history text ", 300)))])]);
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.UnknownBudget, result.Status);
        Assert.Null(result.Count!.EstimatedInputTokens);
        Assert.True(result.Count.HasOpaqueContent);
        Assert.Single(fixture.Writer.Tokens);
        Assert.Single(fixture.Gateway.Requests);
        Assert.True(result.ActiveContext!.Items[0].Content.GetProperty("future").GetProperty("x").GetInt32() == 1);
        ServiceResult<ContextBudgetAssessment> budget = await new ContextBudgetGuard(fixture.Counter).CheckAsync(result.PreparedRequest, Settings(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Unsupported, budget.Error!.Type);
    }

    /// <summary>Непрозрачное содержимое не запрещает независимому counter дать обоснованную полную оценку.</summary>
    [Fact]
    public async Task CustomFullOpaqueEstimateIsPreservedByDi()
    {
        using Fixture fixture = new();
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed([Item("""{"type":"compaction","encrypted_content":"opaque"}""")]));
        fixture.FakeCounter.Evaluate = request => new("custom", 0,
            request.Input.Any(item => item.Content.TryGetProperty("encrypted_content", out _)) ? 50 : 200, true);
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.TargetReached, result.Status);
        Assert.Equal(50, result.Count!.EstimatedInputTokens);
        Assert.Single(fixture.Writer.Tokens);
        Assert.Same(fixture.Counter, fixture.Scope.ServiceProvider.GetRequiredService<IContextTokenCounter>());
    }

    /// <summary>Порог включает равенство, размер не берётся из known при неизвестной оценке.</summary>
    [Theory]
    [InlineData(99, ContextCompactionStatus.NotRequired, 0)]
    [InlineData(100, ContextCompactionStatus.TargetReached, 1)]
    [InlineData(-1, ContextCompactionStatus.UnknownBudget, 0)]
    public async Task ThresholdAndUnknown(long initial, ContextCompactionStatus expected, int calls)
    {
        using Fixture fixture = new();
        fixture.FakeCounter.Evaluate = request => new("custom", 0,
            request.Input.Any(item => Content(item) == "small") ? 10 : initial < 0 ? null : initial, initial < 0);
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(expected, result.Status);
        Assert.Equal(calls, fixture.Gateway.Requests.Count);
    }

    /// <summary>Известное отсутствие уменьшения не заменяет принятое окно.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public async Task NonReductionDoesNotSave(int outputSize)
    {
        using Fixture fixture = new();
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed([Text(new string('x', outputSize))]));
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.NoReduction, result.Status);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Null(result.ActiveContext);
        Assert.Single(fixture.Gateway.Requests);
        Assert.Equal(2, result.PreparedRequest.Input.Count);
    }

    /// <summary>Повторные проходы используют результат предыдущего save и не вызывают providers повторно.</summary>
    [Fact]
    public async Task BoundedPassesUseSavedTokenAndRetainFullBudgetFailure()
    {
        using Fixture fixture = new(maxPasses: 2);
        fixture.Provider.Items = [Text(new string('p', 900))];
        fixture.Gateway.Next = request => Success(ModelResponse.Completed([Text(new string('x', Content(request.Input[0]).Length - 20))]));
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.PassLimitReached, result.Status);
        Assert.Equal(2, result.Passes);
        Assert.Equal(2, fixture.Writer.Tokens.Count);
        Assert.Equal(fixture.Writer.Tokens[0].Revision + 1, fixture.Writer.Tokens[1].Revision);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(160, Content(result.ActiveContext!.Items[0]).Length);
        Assert.Equal(ServiceErrorType.Rejected, (await new ContextBudgetGuard(fixture.Counter)
            .CheckAsync(result.PreparedRequest, Settings(), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Ожидаемый отказ второго прохода оставляет первый сохранённый контекст и исходную ошибку.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondPassFailureRetainsFirstAcceptedWindow(bool failSave)
    {
        using Fixture fixture = new();
        ServiceError failure = new(ServiceErrorType.Conflict, "conflict");
        fixture.Gateway.Next = _ => !failSave && fixture.Gateway.Requests.Count == 2
            ? Task.FromResult(ServiceResult<ModelResponse>.Fail(failure))
            : Success(ModelResponse.Completed([Text(new string('x', fixture.Gateway.Requests.Count == 1 ? 150 : 120))]));
        if (failSave) { fixture.Writer.FailureAt = 2; fixture.Writer.Failure = failure; }
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.Failed, result.Status);
        Assert.Same(failure, result.Error);
        Assert.Equal(150, Content(result.ActiveContext!.Items[0]).Length);
        Assert.Same(fixture.Writer.Accepted, result.ActiveContext.Compaction);
        Assert.Equal(11, result.Token.Revision);
        Assert.Equal(2, fixture.Gateway.Requests.Count);
    }

    /// <summary>Stale/incarnation/owner/expired ошибки writer передаются без refresh или retry.</summary>
    [Theory]
    [InlineData(ServiceErrorType.Conflict)]
    [InlineData(ServiceErrorType.Expired)]
    [InlineData(ServiceErrorType.Forbidden)]
    [InlineData(ServiceErrorType.NotFound)]
    public async Task SaveFailureDoesNotActivate(ServiceErrorType type)
    {
        using Fixture fixture = new();
        fixture.Writer.FailureAt = 1;
        fixture.Writer.Failure = new(type, "failure");
        DialogSnapshot snapshot = Snapshot();
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Same(fixture.Writer.Failure, result.Error);
        Assert.Same(snapshot.Token, result.Token);
        Assert.Null(result.ActiveContext);
        Assert.Null(fixture.Writer.Accepted);
        Assert.Single(fixture.Writer.Tokens);
    }

    /// <summary>Свежий UTC после HTTP и после counter исключает запись ровно в expiry.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactExpiryAfterExternalAwaitDoesNotSave(bool expireInCounter)
    {
        using Fixture fixture = new();
        if (expireInCounter)
        {
            fixture.FakeCounter.Evaluate = request =>
            {
                if (request.Input.Any(item => Content(item) == "small")) { fixture.Time.Now = NOW.AddHours(1); }
                return new("custom", 0, request.Input.Sum(item => (long)Content(item).Length), false);
            };
        }
        else { fixture.Gateway.Next = _ => { fixture.Time.Now = NOW.AddHours(1); return Success(ModelResponse.Completed([Text("small")])); }; }
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Expired, result.Error!.Type);
        Assert.Empty(fixture.Writer.Tokens);
    }

    /// <summary>Save получает время после сети, а не исходный timestamp.</summary>
    [Fact]
    public async Task SaveReceivesFreshTime()
    {
        using Fixture fixture = new();
        fixture.Gateway.Next = _ => { fixture.Time.Now = NOW.AddMinutes(1); return Success(ModelResponse.Completed([Text("small")])); };
        await fixture.Run(ct: TestContext.Current.CancellationToken);
        Assert.Equal(NOW.AddMinutes(1), Assert.Single(fixture.Writer.Accesses).NowUtc);
    }

    /// <summary>Incomplete/failed/canceled/empty и незакрытая функция никогда не сохраняются.</summary>
    [Theory]
    [InlineData("incomplete")]
    [InlineData("failed")]
    [InlineData("canceled")]
    [InlineData("empty")]
    [InlineData("call")]
    [InlineData("continuation")]
    public async Task InvalidCandidateRetainsHistory(string kind)
    {
        using Fixture fixture = new();
        ModelResponse report = kind switch
        {
            "incomplete" => ModelResponse.Incomplete([Text("small")]),
            "failed" => ModelResponse.Failed([Text("small")], new(ServiceErrorType.Rejected, "failed")),
            "canceled" => ModelResponse.Canceled([Text("small")]),
            "empty" => ModelResponse.Completed([]),
            "call" => ModelResponse.Completed([Item("""{"type":"function_call","call_id":"x","arguments":"{"}""")]),
            _ => ModelResponse.Completed([Text("small")], continuation: new(Json("{}")))
        };
        fixture.Gateway.Next = _ => Success(report);
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.Failed, result.Status);
        Assert.Same(report, result.LastResponse);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Null(result.ActiveContext);
    }

    /// <summary>Terminal prefix0 допустим для ранее принятого окна; текущий turn не объявляется покрытым.</summary>
    [Fact]
    public async Task PrefixZeroRetainsWholeTail()
    {
        using Fixture fixture = new();
        StoredDialogContext active = new(1, 0, ModelResponse.Completed([Text(new string('x', 200))]));
        ContextCompactionResult result = (await fixture.Run(Snapshot([Turn(1, DialogTurnStatus.InProgress, [Text("tail")])], active), ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(0, result.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(new[] { "provider", "small", "tail" }, result.PreparedRequest.Input.Select(Content));
    }

    /// <summary>Только transient контекст не создаёт фиктивного persisted окна.</summary>
    [Fact]
    public async Task TransientOnlyCannotBecomePersistedWindow()
    {
        using Fixture fixture = new();
        fixture.Provider.Items = [Text(new string('x', 200))];
        ContextCompactionResult result = (await fixture.Run(Snapshot([]), ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.NoPersistableHistory, result.Status);
        Assert.Empty(fixture.Gateway.Requests);
        Assert.Empty(fixture.Writer.Tokens);
    }

    /// <summary>Полная пара через границу transient не позволяет отправить незакрытый persisted call.</summary>
    [Fact]
    public async Task FunctionPairAcrossPersistableBoundaryFailsBeforeHttp()
    {
        using Fixture fixture = new();
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed,
            [Text(new string('x', 200)), Item("""{"type":"function_call","call_id":"a","arguments":"{}"}""")])]);
        ModelRequest request = Request([Item("""{"type":"function_call_output","call_id":"a","output":"ok"}""")]);
        ContextCompactionResult result = (await fixture.Run(snapshot, request, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Empty(fixture.Gateway.Requests);
        Assert.Empty(fixture.Writer.Tokens);
    }

    /// <summary>Чужой owner, повреждённый prefix и истечение блокируют providers до I/O.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("expiry")]
    [InlineData("prefix")]
    public async Task InvalidSnapshotFailsBeforeProviders(string kind)
    {
        using Fixture fixture = new();
        DialogSnapshot original = Snapshot();
        DialogSnapshot snapshot = new(original.Token, kind == "owner" ? DialogOwnerId.From("other") : OWNER,
            original.CreatedAtUtc, kind == "expiry" ? NOW : original.ExpiresAtUtc, 100, original.Turns,
            kind == "prefix" ? new(1, 2, ModelResponse.Completed([Text("old")])) : null);
        Assert.False((await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Success);
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Empty(fixture.Gateway.Requests);
    }

    /// <summary>Явная caller cancellation и unexpected exception не подменяются успешным отчётом.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndUnexpectedIoPropagate(bool cancel)
    {
        using Fixture fixture = new();
        using CancellationTokenSource source = new();
        IOException error = new("private");
        fixture.Gateway.Next = _ =>
        {
            if (cancel) { source.Cancel(); return Success(ModelResponse.Completed([Text("small")])); }
            return Task.FromException<ServiceResult<ModelResponse>>(error);
        };
        if (cancel) { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Run(ct: source.Token)); }
        else { Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => fixture.Run(ct: TestContext.Current.CancellationToken))); }
        Assert.Empty(fixture.Writer.Tokens);
    }

    /// <summary>Неизвестное значение причины остановки не создаёт допустимый отчёт.</summary>
    [Fact]
    public void ResultRejectsInvalidStatus() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new ContextCompactionResult((ContextCompactionStatus)999, Snapshot().Token, null, Request(), null, 0));

    /// <summary>Окно не публикуется до завершения save; ожидание не повторяет HTTP.</summary>
    [Fact]
    public async Task ActivationWaitsForSuccessfulSave()
    {
        using Fixture fixture = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Writer.BeforeSave = async ct => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        using CancellationTokenSource caller = new();
        Task<ServiceResult<ContextCompactionResult>> pending = fixture.Run(ct: caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(pending.IsCompleted);
            Assert.Null(fixture.Writer.Accepted);
            Assert.Single(fixture.Gateway.Requests);
            release.TrySetResult();
            ContextCompactionResult result = (await pending).Data!;
            Assert.Same(fixture.Writer.Accepted, result.ActiveContext!.Compaction);
        }
        finally
        {
            caller.Cancel();
            release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (caller.IsCancellationRequested) { }
        }
    }

    /// <summary>Typed gateway failure сохраняет identity даже при поздней отмене.</summary>
    [Fact]
    public async Task TypedFailureWinsLateCancellation()
    {
        using Fixture fixture = new();
        using CancellationTokenSource caller = new();
        ServiceError error = new(ServiceErrorType.Forbidden, "denied");
        fixture.Gateway.Next = _ => { caller.Cancel(); return Task.FromResult(ServiceResult<ModelResponse>.Fail(error)); };
        ContextCompactionResult result = (await fixture.Run(ct: caller.Token)).Data!;
        Assert.Same(error, result.Error);
        Assert.Empty(fixture.Writer.Tokens);
    }

    /// <summary>Отказ подсчёта кандидата не активирует непроверенное окно и не скрывает исходную ошибку.</summary>
    [Fact]
    public async Task CandidateCounterFailureDoesNotSave()
    {
        using Fixture fixture = new();
        ServiceError error = new(ServiceErrorType.Unsupported, "counter failed");
        fixture.FakeCounter.FailureFor = request => request.Input.Any(item => Content(item) == "small") ? error : null;
        ContextCompactionResult result = (await fixture.Run(ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Same(error, result.Error);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Equal(208, result.Count!.EstimatedInputTokens);
    }

    /// <summary>Превышение input budget самого compact не отправляет заведомо недопустимый запрос.</summary>
    [Fact]
    public async Task CompactPayloadBudgetCheckedBeforeHttp()
    {
        using Fixture fixture = new();
        ContextCompactionResult result = (await fixture.Run(Snapshot([Turn(1, DialogTurnStatus.Completed,
            [Text(new string('x', 991))])]), ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Empty(fixture.Gateway.Requests);
    }

    /// <summary>Все terminal статусы входят в непрерывный prefix без объявления успешного model lifecycle.</summary>
    [Theory]
    [InlineData(DialogTurnStatus.Completed)]
    [InlineData(DialogTurnStatus.Incomplete)]
    [InlineData(DialogTurnStatus.Failed)]
    [InlineData(DialogTurnStatus.Canceled)]
    public async Task AllTerminalStatusesCanBeCovered(DialogTurnStatus status)
    {
        using Fixture fixture = new();
        ContextCompactionResult result = (await fixture.Run(Snapshot([Turn(1, status, [Text(new string('h', 200))])]), ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(1, result.ActiveContext!.ThroughTurnSequence);
    }

    /// <summary>Явный server continuation не теряется при проекции, а отклоняется до providers.</summary>
    [Fact]
    public async Task ContinuationRejectedBeforeProviders()
    {
        using Fixture fixture = new();
        ModelRequest request = new("gpt-5", "medium", "", [], [], new(Json("{}")));
        Assert.Equal(ServiceErrorType.Unsupported, (await fixture.Run(request: request, ct: TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(0, fixture.Provider.Calls);
    }

    /// <summary>Баланс повторных ID не разрешает замену исходной FIFO-пары.</summary>
    [Fact]
    public async Task RepeatedAssociationDifferentiatingRegression()
    {
        using Fixture fixture = new();
        CanonicalModelItem callA = PairCall("A");
        CanonicalModelItem callB = PairCall("B");
        CanonicalModelItem outputA = PairOutput("A");
        CanonicalModelItem outputB = PairOutput("B");
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed,
            [Text(new string('h', 200)), callA, callB, outputA, outputB])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed([callB, outputA]));
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Null(result.ActiveContext);
        Assert.Equal(snapshot.Token, result.Token);
        Assert.Equal(5, snapshot.Turns[0].Items.Count);
    }

    /// <summary>Сохранённые occurrences проходят последующий builder/executor без повторного открытия handler.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task RepeatedAssociationRetainsOriginalPairsWithoutReplay(int selection, bool alternating)
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), b = PairCall("B"), oa = PairOutput("A"), ob = PairOutput("B");
        CanonicalModelItem[] pairs = alternating ? [a, oa, b, ob] : [a, b, oa, ob];
        CanonicalModelItem[] retained = selection == 0 ? [a, oa] : selection == 1 ? [b, ob] : pairs;
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, [Text(new string('h', 200)), .. pairs])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed(retained));
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.TargetReached, result.Status);
        Assert.Single(fixture.Writer.Tokens);
        Assert.Same(fixture.Writer.Accepted, result.ActiveContext!.Compaction);
        DialogSnapshot saved = new(result.Token, OWNER, snapshot.CreatedAtUtc, snapshot.ExpiresAtUtc,
            snapshot.ContentBytes, snapshot.Turns, result.ActiveContext);
        ModelRequest built = (await fixture.Root.GetRequiredService<ContextBuilder>()
            .BuildAsync(new(ID, OWNER, Guid.NewGuid(), "agent"), saved, Request(), NOW, cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(retained.Select(item => item.Content.GetRawText()), built.Input.Skip(1).Select(item => item.Content.GetRawText()));
        Assert.Equal(snapshot.ExpiresAtUtc, saved.ExpiresAtUtc);
        Assert.Same(snapshot.Turns[0], saved.Turns[0]);
        NoReplayRegistry registry = new();
        ToolExecutor executor = new(registry, fixture.Time);
        ToolExecutionSession session = executor.CreateSession(new(ID, OWNER, Guid.NewGuid(), "agent"), result.Token,
            saved.ExpiresAtUtc, ["GetOrderStatus"], new(8, 16, 1, TimeSpan.FromMinutes(1)));
        ToolExecutionBatch batch = (await executor.ExecuteAsync(session, new(Guid.NewGuid(), result.ActiveContext.Compaction), cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Empty(batch.Results);
        Assert.Equal(0, registry.Opened);
    }

    /// <summary>Подмена, повтор, неполная и неизвестная association не проходят save даже при локальном балансе.</summary>
    [Theory]
    [InlineData("swapped")]
    [InlineData("rewritten-call")]
    [InlineData("rewritten-output")]
    [InlineData("duplicated")]
    [InlineData("incomplete")]
    [InlineData("orphan")]
    public async Task RepeatedAssociationInvalidSubsetRefuses(string kind)
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), b = PairCall("B"), oa = PairOutput("A"), ob = PairOutput("B");
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed,
            [Text(new string('h', 200)), a, b, oa, ob])]);
        CanonicalModelItem[] candidate = kind switch
        {
            "swapped" => [a, b, ob, oa],
            "rewritten-call" => [PairCall("C"), oa],
            "rewritten-output" => [a, PairOutput("C")],
            "duplicated" => [a, oa, a, oa],
            "incomplete" => [a, b, oa],
            _ => [oa]
        };
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed(candidate));
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.Failed, result.Status);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Equal(snapshot.Token, result.Token);
        Assert.Null(result.ActiveContext);
    }

    /// <summary>Разные ID допускают исходный обратный порядок результатов без догадки о новых парах.</summary>
    [Fact]
    public async Task RepeatedAssociationPreservesCrossIdOutputOrder()
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), oa = PairOutput("A");
        CanonicalModelItem b = Item("""{"type":"function_call","call_id":"y","name":"GetOrderStatus","arguments":"{}"}""");
        CanonicalModelItem ob = Item("""{"type":"function_call_output","call_id":"y","output":"B"}""");
        CanonicalModelItem[] pairs = [a, b, ob, oa];
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, [Text(new string('h', 200)), .. pairs])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed(pairs));
        Assert.Equal(ContextCompactionStatus.TargetReached, (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!.Status);
        Assert.Single(fixture.Writer.Tokens);
    }

    /// <summary>Opaque не раскрывает скрытую association, но сохраняется вместе с проверенной внешней парой.</summary>
    [Fact]
    public async Task RepeatedAssociationOpaqueRetainsKnownPairAndUnknownBudget()
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), oa = PairOutput("A");
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, [Text(new string('h', 200)), a, oa])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed(
            [Item("""{"type":"compaction","encrypted_content":"opaque"}"""), a, oa]));
        fixture.FakeCounter.Evaluate = request => request.Input.Any(item => item.Content.TryGetProperty("encrypted_content", out _))
            ? new("custom", 5, null, true) : new("custom", 0, 200, false);
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.UnknownBudget, result.Status);
        Assert.Single(fixture.Writer.Tokens);
        Assert.Null(result.Count!.EstimatedInputTokens);
        Assert.Equal(ServiceErrorType.Unsupported, (await new ContextBudgetGuard(fixture.Counter)
            .CheckAsync(result.PreparedRequest, Settings(), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Одна из полностью одинаковых occurrences не имеет доказанной исходной association.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedAssociationAmbiguousSubsetRefuses(bool alternating)
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), oa = PairOutput("A");
        CanonicalModelItem[] pairs = alternating ? [a, oa, a, oa] : [a, a, oa, oa];
        StoredDialogContext previous = new(1, 1, ModelResponse.Completed([Text(new string('h', 200)), .. pairs]));
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, pairs)], previous);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed([a, oa]));
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Same(previous, result.ActiveContext);
        Assert.Equal(snapshot.Token, result.Token);
        Assert.Equal(4, snapshot.Turns[0].Items.Count);
    }

    /// <summary>Полная одинаковая последовательность и одинаковые calls с разными outputs остаются однозначными.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedAssociationIdenticalCallsHaveUniqueControls(bool keepAll)
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), oa = PairOutput("A"), ob = keepAll ? PairOutput("A") : PairOutput("B");
        CanonicalModelItem[] pairs = [a, a, oa, ob];
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, [Text(new string('h', 200)), .. pairs])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed(keepAll ? pairs : [a, ob]));
        Assert.Equal(ContextCompactionStatus.TargetReached, (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!.Status);
        Assert.Single(fixture.Writer.Tokens);
    }

    /// <summary>Ошибка association второго прохода оставляет token и окно первого save.</summary>
    [Fact]
    public async Task RepeatedAssociationSecondPassRetainsAcceptedWindow()
    {
        using Fixture fixture = new();
        CanonicalModelItem a = PairCall("A"), b = PairCall("B"), oa = PairOutput("A"), ob = PairOutput("B");
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed,
            [Text(new string('h', 200)), a, b, oa, ob])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed(fixture.Gateway.Requests.Count == 1
            ? [Text(new string('s', 150)), a, b, oa, ob] : [b, oa]));
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Single(fixture.Writer.Tokens);
        Assert.Equal(11, result.Token.Revision);
        Assert.Same(fixture.Writer.Accepted, result.ActiveContext!.Compaction);
        Assert.Equal(5, result.ActiveContext.Items.Count);
        Assert.Equal(2, fixture.Gateway.Requests.Count);
        Assert.Equal(snapshot.ExpiresAtUtc, NOW.AddHours(1));
    }

    /// <summary>Незакрытый исходный occurrence блокирует compact без gateway/save.</summary>
    [Fact]
    public async Task RepeatedAssociationIncompletePrefixRefuses()
    {
        using Fixture fixture = new();
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed,
            [Text(new string('h', 200)), PairCall("A"), PairCall("B"), PairOutput("A")])]);
        ServiceResult<ContextCompactionResult> result = await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Empty(fixture.Gateway.Requests);
        Assert.Empty(fixture.Writer.Tokens);
        Assert.Equal(4, snapshot.Turns[0].Items.Count);
    }

    /// <summary>Полный бюджет включает providers после сохранения корректного subset.</summary>
    [Fact]
    public async Task RepeatedAssociationSubsetDoesNotReplaceFullBudget()
    {
        using Fixture fixture = new(maxPasses: 1);
        fixture.Provider.Items = [Text(new string('p', 900))];
        CanonicalModelItem a = PairCall("A"), oa = PairOutput("A");
        DialogSnapshot snapshot = Snapshot([Turn(1, DialogTurnStatus.Completed, [Text(new string('h', 200)), a, oa])]);
        fixture.Gateway.Next = _ => Success(ModelResponse.Completed([Text(new string('s', 150)), a, oa]));
        ContextCompactionResult result = (await fixture.Run(snapshot, ct: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ContextCompactionStatus.PassLimitReached, result.Status);
        Assert.Equal(1050, result.Count!.EstimatedInputTokens);
        Assert.Single(fixture.Writer.Tokens);
        Assert.Equal(ServiceErrorType.Rejected, (await new ContextBudgetGuard(fixture.Counter)
            .CheckAsync(result.PreparedRequest, Settings(), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Закрытые сохранённые пары не должны открывать scope и повторять бизнес-действие.</summary>
    private class NoReplayRegistry : IToolRegistry
    {
        internal int Opened;
        /// <inheritdoc/>
        public IReadOnlyList<ModelToolDefinition> Definitions => [];
        /// <inheritdoc/>
        public IToolHandlerScope? OpenScope(string name)
        {
            Opened++;
            throw new InvalidOperationException("Повтор handler недопустим.");
        }
    }

    /// <summary>Создаёт различимый вызов с повторным внешним ID.</summary>
    private static CanonicalModelItem PairCall(string value) => new(JsonSerializer.SerializeToElement(new
        { type = "function_call", call_id = "x", name = "GetOrderStatus", arguments = JsonSerializer.Serialize(new { value }) }));
    /// <summary>Создаёт различимый результат с повторным внешним ID.</summary>
    private static CanonicalModelItem PairOutput(string value) => new(JsonSerializer.SerializeToElement(new
        { type = "function_call_output", call_id = "x", output = value }));

    /// <summary>Создаёт стандартное неизменяемое состояние диалога.</summary>
    private static DialogSnapshot Snapshot(IEnumerable<StoredDialogTurn>? turns = null, StoredDialogContext? active = null) =>
        new(new(ID, Guid.NewGuid(), 10), OWNER, NOW.AddHours(-1), NOW.AddHours(1), 100,
            turns ?? [Turn(1, DialogTurnStatus.Completed, [Text(new string('h', 200))])], active);
    /// <summary>Создаёт сохранённый turn без дублированных model steps.</summary>
    private static StoredDialogTurn Turn(long sequence, DialogTurnStatus status, IEnumerable<CanonicalModelItem> items) => new(Guid.NewGuid(), sequence, status, items, []);
    /// <summary>Создаёт новый вход, отдельно от истории.</summary>
    private static ModelRequest Request(IEnumerable<CanonicalModelItem>? input = null) => new("gpt-5", "medium", "", input ?? [], []);
    /// <summary>Создаёт проверяемые модельные лимиты.</summary>
    private static ModelSettingsSnapshot Settings() => new(new("gpt-5", true, 2000, 1000, 100,
        ["medium"], "medium", ["text"], true, true, true, true, false), "medium", 100, 10);
    /// <summary>Фиксирует JSON до освобождения исходного документа.</summary>
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    /// <summary>Создаёт канонический item.</summary>
    private static CanonicalModelItem Item(string json) => new(Json(json));
    /// <summary>Создаёт известное текстовое сообщение.</summary>
    private static CanonicalModelItem Text(string text) => new(JsonSerializer.SerializeToElement(new { type = "message", role = "user", content = text }));
    /// <summary>Читает только текстовый fixture payload; иной item не получает вымышленную оценку production.</summary>
    private static string Content(CanonicalModelItem item) => item.Content.TryGetProperty("content", out JsonElement text) ? text.GetString()! : "";
    /// <summary>Возвращает готовый lifecycle report.</summary>
    private static Task<ServiceResult<ModelResponse>> Success(ModelResponse response) => Task.FromResult(ServiceResult<ModelResponse>.Ok(response));

    /// <summary>Изолированный composition root без host, БД и HTTP.</summary>
    private class Fixture : IDisposable
    {
        internal readonly Provider Provider = new();
        internal readonly Counter FakeCounter = new();
        internal readonly Gateway Gateway = new();
        internal readonly Writer Writer = new();
        internal readonly Clock Time = new();
        internal readonly ServiceProvider Root;
        internal readonly IServiceScope Scope;
        internal readonly IContextTokenCounter Counter;
        /// <summary>Собирает реальные DI/scenario и явный ordered provider selection.</summary>
        internal Fixture(int maxPasses = 3, bool actualCounter = false)
        {
            Counter = actualCounter ? new ContextTokenCounter() : FakeCounter;
            ServiceCollection services = new();
            services.AddSingleton(new ContextBuilder([Provider]));
            services.AddSingleton(Counter);
            services.AddSingleton<IModelGateway>(Gateway);
            services.AddSingleton<IDialogContextWriter>(Writer);
            services.AddSingleton<TimeProvider>(Time);
            services.AddOptions<ContextCompactionOptions>().Configure(value => value.MaxPasses = maxPasses);
            services.AddAgentBridgeTokenization();
            services.AddAgentBridgeCompaction();
            Root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Scope = Root.CreateScope();
        }
        /// <summary>Вызывает публичный прикладной API.</summary>
        internal Task<ServiceResult<ContextCompactionResult>> Run(DialogSnapshot? snapshot = null, ModelRequest? request = null,
            CancellationToken ct = default) => Scope.ServiceProvider.GetRequiredService<ContextCompactor>()
            .CompactAsync(new(ID, OWNER, Guid.NewGuid(), "agent"), snapshot ?? Snapshot(), request ?? Request(), Settings(), new("synthetic-key"), ct);
        /// <inheritdoc/>
        public void Dispose() { Scope.Dispose(); Root.Dispose(); }
    }

    /// <inheritdoc/>
    private class Provider : IContextProvider
    {
        internal int Calls;
        internal CanonicalModelItem[] Items = [Text("provider")];
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(ServiceResult<ContextContribution>.Ok(new(Items)));
        }
    }
    /// <inheritdoc/>
    private class Counter : IContextTokenCounter
    {
        internal Func<ModelRequest, ServiceError?> FailureFor = _ => null;
        internal Func<ModelRequest, ContextTokenCount> Evaluate = request => new("custom", 0,
            request.Input.Sum(item => (long)Content(item).Length), false);
        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            ServiceError? error = FailureFor(request);
            return Task.FromResult(error is null ? ServiceResult<ContextTokenCount>.Ok(Evaluate(request)) : ServiceResult<ContextTokenCount>.Fail(error));
        }
    }
    /// <inheritdoc/>
    private class Gateway : IModelGateway
    {
        internal readonly List<ModelRequest> Requests = [];
        internal Func<ModelRequest, Task<ServiceResult<ModelResponse>>> Next = _ => Success(ModelResponse.Completed([Text("small")]));
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Next(request);
        }
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
    /// <inheritdoc/>
    private class Writer : IDialogContextWriter
    {
        internal readonly List<DialogWriteToken> Tokens = [];
        internal readonly List<DialogAccess> Accesses = [];
        internal int FailureAt;
        internal ServiceError Failure = new(ServiceErrorType.Conflict, "conflict");
        internal ModelResponse? Accepted;
        internal Func<CancellationToken, Task>? BeforeSave;
        /// <inheritdoc/>
        public async Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected, long throughTurnSequence,
            ModelResponse compaction, CancellationToken cancellationToken = default)
        {
            Tokens.Add(expected);
            Accesses.Add(access);
            if (BeforeSave is not null) { await BeforeSave(cancellationToken); }
            if (Tokens.Count == FailureAt) { return ServiceResult<DialogWriteToken>.Fail(Failure); }
            Accepted = compaction;
            return ServiceResult<DialogWriteToken>.Ok(new(expected.DialogId, expected.IncarnationId, expected.Revision + 1));
        }
    }
    /// <inheritdoc/>
    private class Clock : TimeProvider
    {
        internal DateTimeOffset Now = NOW;
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
