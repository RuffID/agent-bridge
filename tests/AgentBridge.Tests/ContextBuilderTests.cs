using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Проверяет публичную композицию контекста без модели, хранилища, сервера и tokenizer.</summary>
public class ContextBuilderTests
{
    private static readonly DateTimeOffset NOW_UTC = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogId DIALOG_ID = DialogId.From(Guid.NewGuid());
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From("tenant/user");

    /// <summary>Подготовленный запрос сохраняет инструкции отдельно, исходные роли и полные параметры/инструменты.</summary>
    [Fact]
    public async Task CompleteRequestPreservesOrderRolesOpaqueAndControlsWithoutStepDuplication()
    {
        CanonicalModelItem system = Item("""{"type":"message","role":"system","content":"provider-system","future":1}""");
        CanonicalModelItem developer = Item("""{"type":"message","role":"developer","content":"provider-developer"}""");
        CanonicalModelItem history = Item("""{"type":"message","role":"user","content":"history"}""");
        CanonicalModelItem assistant = Item("""{"type":"message","role":"assistant","content":"answer","future":{"n":null}}""");
        CanonicalModelItem opaque = Item("""{"type":"reasoning","encrypted_content":"opaque+/==","future":[true,null]}""");
        CanonicalModelItem fresh = Item("""{"type":"message","role":"user","content":"new"}""");
        ModelContinuation continuation = new(Json("""{"binding":"synthetic","future":{"state":42}}"""));
        ModelRequestParameters parameters = new(Json("""{"include":["reasoning.encrypted_content"],"metadata":{"x":"y"}}"""));
        ModelToolDefinition tool = new("GetOrder", "Получить заказ", Json("""{"type":"object","future":{"n":null}}"""), true);
        ModelRequest request = new("exact-model", "high", "Системные инструкции агента", [fresh], [tool], continuation, parameters);
        ModelResponse response = ModelResponse.Completed([assistant, opaque],
            new(Json("""{"id":"response-id","output":[],"envelope_only":true}""")), continuation);
        StoredDialogTurn turn = new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [history, assistant, opaque],
            [new(Guid.NewGuid(), response)]);
        List<string> order = [];
        Provider first = new((_, _) => { order.Add("first"); return Success([system]); });
        Provider second = new((_, _) => { order.Add("second"); return Success([developer]); });
        ApplicationCallContext call = Call("selected-agent");

        ServiceResult<ModelRequest> result = await new ContextBuilder([first, second]).BuildAsync(call, Snapshot([turn]), request, NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        ModelRequest prepared = Assert.IsType<ModelRequest>(result.Data);
        Assert.True(result.Success);
        Assert.Equal(["first", "second"], order);
        Assert.Equal(new[] { system, developer, history, assistant, opaque, fresh }, prepared.Input);
        Assert.Equal("Системные инструкции агента", prepared.Instructions);
        Assert.Equal("exact-model", prepared.Model);
        Assert.Equal("high", prepared.ReasoningEffort);
        Assert.Same(tool, Assert.Single(prepared.Tools));
        Assert.Same(parameters, prepared.Parameters);
        Assert.Same(continuation, prepared.Continuation);
        Assert.Equal("opaque+/==", prepared.Input[4].Content.GetProperty("encrypted_content").GetString());
        Assert.Equal(system.Content.GetRawText(), prepared.Input[0].Content.GetRawText());
        foreach (Provider provider in new[] { first, second })
        {
            Assert.Same(call, provider.Request!.Call);
            Assert.Equal("selected-agent", provider.Request.Call.AgentId);
            Assert.Same(fresh, Assert.Single(provider.Request.NewInput));
        }
        Assert.Same(parameters, request.Parameters);
        Assert.Single(request.Input);
    }

    /// <summary>Каждый допустимый terminal prefix замещается окном, а текущий InProgress turn остаётся целиком.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CompressedContextUsesExactlyItsTerminalPrefixAndFullTail(int through)
    {
        CanonicalModelItem compact = Item("""{"type":"compaction","encrypted_content":"compact-opaque","future":9}""");
        CanonicalModelItem fresh = Message("new");
        StoredDialogTurn[] turns =
        [
            Turn(1, DialogTurnStatus.Completed, [Message("one")]),
            Turn(2, DialogTurnStatus.Failed, [Message("two")]),
            Turn(3, DialogTurnStatus.Canceled, [Message("three")]),
            Turn(4, DialogTurnStatus.InProgress, [Message("current"), FunctionCall("current-call"), FunctionOutput("current-call")])
        ];
        ModelContinuation compactContinuation = new(Json("""{"compact_metadata":true}"""));
        StoredDialogContext context = new(7, through, ModelResponse.Completed([compact],
            new(Json("""{"envelope_only":true}""")), compactContinuation));
        DialogSnapshot dialog = Snapshot(turns, context);

        ServiceResult<ModelRequest> result = await new ContextBuilder([]).BuildAsync(Call(), dialog, Request([fresh]), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(new[] { compact }.Concat(turns.Where(turn => turn.Sequence > through).SelectMany(turn => turn.Items)).Append(fresh),
            result.Data!.Input);
        Assert.Null(result.Data.Continuation);
        Assert.Equal(through, dialog.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(4, dialog.Turns.Count);
        Assert.Equal(3, dialog.Turns[3].Items.Count);
        Assert.Equal(NOW_UTC.AddDays(1), dialog.ExpiresAtUtc);
    }

    /// <summary>Пары функций проверяются после объединения, в том числе на всех границах источников.</summary>
    [Theory]
    [InlineData("provider-window")]
    [InlineData("window-tail")]
    [InlineData("tail-new")]
    [InlineData("window-new")]
    public async Task FunctionPairMayCrossCompositionBoundaries(string boundary)
    {
        CanonicalModelItem call = FunctionCall("cross-call");
        CanonicalModelItem output = FunctionOutput("cross-call");
        CanonicalModelItem[] providerItems = boundary == "provider-window" ? [call] : [];
        CanonicalModelItem[] window = boundary switch { "provider-window" => [output], "window-tail" or "window-new" => [call], _ => [] };
        CanonicalModelItem[] tail = boundary switch { "window-tail" => [output], "tail-new" => [call], _ => [] };
        CanonicalModelItem[] fresh = boundary is "tail-new" or "window-new" ? [output] : [];
        Provider provider = new((_, _) => Success(providerItems));
        StoredDialogContext context = new(1, 0, ModelResponse.Completed(window));

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(Call(),
            Snapshot([Turn(1, DialogTurnStatus.InProgress, tail)], context), Request(fresh), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(new[] { call, output }, result.Data!.Input);
        Assert.Equal(call.Content.GetRawText(), result.Data.Input[0].Content.GetRawText());
        Assert.Equal(output.Content.GetRawText(), result.Data.Input[1].Content.GetRawText());
    }

    /// <summary>Несколько interleaved вызовов сохраняют полный исходный порядок, аргументы и результаты.</summary>
    [Fact]
    public async Task InterleavedFunctionPairsAndUnknownOpaqueItemsArePreserved()
    {
        CanonicalModelItem unknown = Item("""{"type":"future-state","future":{"type":"function_call","arguments":"partial"}}""");
        CanonicalModelItem opaque = Item("""{"type":"compaction","encrypted_content":"opaque","future":{"call_id":"hidden"}}""");
        CanonicalModelItem[] items = [FunctionCall("A"), FunctionCall("B"), unknown, FunctionOutput("B"), opaque, FunctionOutput("A")];

        ServiceResult<ModelRequest> result = await new ContextBuilder([]).BuildAsync(Call(), Snapshot(), Request(items), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(items.Select(item => item.Content.GetRawText()), result.Data!.Input.Select(item => item.Content.GetRawText()));
    }

    /// <summary>Неполный call отклоняет подготовку на любом lifecycle обращения и сохраняет всю историю/отчёт.</summary>
    [Theory]
    [InlineData(DialogTurnStatus.InProgress)]
    [InlineData(DialogTurnStatus.Incomplete)]
    [InlineData(DialogTurnStatus.Failed)]
    [InlineData(DialogTurnStatus.Canceled)]
    [InlineData(DialogTurnStatus.Completed)]
    public async Task CallWithoutOutputRefusesRequestWithoutChangingPartialHistory(DialogTurnStatus status)
    {
        CanonicalModelItem call = Item("""{"type":"function_call","call_id":"pending","name":"GetOrder","arguments":"{\"orderId\":","future":true}""");
        ModelResponse response = ModelResponse.Incomplete([call]);
        StoredDialogTurn turn = new(Guid.NewGuid(), 1, status, [Message("user"), call], [new(Guid.NewGuid(), response)]);
        DialogSnapshot dialog = Snapshot([turn]);
        DialogWriteToken token = dialog.Token;

        ServiceResult<ModelRequest> result = await new ContextBuilder([]).BuildAsync(Call(), dialog, Request(), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.DoesNotContain("pending", result.Error.Message);
        Assert.DoesNotContain("orderId", result.Error.Message);
        Assert.Same(token, dialog.Token);
        Assert.Equal(status, turn.Status);
        Assert.Equal(2, turn.Items.Count);
        Assert.Same(response, Assert.Single(turn.ModelSteps).Response);
        Assert.Equal(ModelResponseStatus.Incomplete, response.Status);
        Assert.Equal(call.Content.GetRawText(), turn.Items[1].Content.GetRawText());
    }

    /// <summary>Отсутствие результата выявляется во всех источниках, включая разрешённый вклад провайдера.</summary>
    [Theory]
    [InlineData("provider")]
    [InlineData("window")]
    [InlineData("new")]
    public async Task MissingFunctionResultIsRejectedFromEverySource(string source)
    {
        CanonicalModelItem call = FunctionCall("missing");
        Provider provider = new((_, _) => Success(source == "provider" ? [call] : []));
        StoredDialogContext context = new(1, 0, ModelResponse.Completed(source == "window" ? [call] : []));

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(Call(), Snapshot(context: context),
            Request(source == "new" ? [call] : []), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
    }

    /// <summary>Повреждённые известные пары не исправляются и не публикуют содержимое аргументов.</summary>
    [Theory]
    [InlineData("missing-call-id")]
    [InlineData("invalid-output-id")]
    [InlineData("orphan-output")]
    [InlineData("duplicate-output")]
    [InlineData("output-before-call")]
    [InlineData("case-mismatch")]
    public async Task MalformedKnownFunctionPairsAreRejected(string shape)
    {
        CanonicalModelItem call = FunctionCall("A");
        CanonicalModelItem output = FunctionOutput("A");
        CanonicalModelItem[] items = shape switch
        {
            "missing-call-id" => [Item("""{"type":"function_call","arguments":"sensitive"}""")],
            "invalid-output-id" => [call, Item("""{"type":"function_call_output","call_id":42,"output":"sensitive"}""")],
            "orphan-output" => [output],
            "duplicate-output" => [call, output, output],
            "output-before-call" => [output, call],
            "case-mismatch" => [call, FunctionOutput("a")],
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        ServiceResult<ModelRequest> result = await new ContextBuilder([]).BuildAsync(Call(), Snapshot(), Request(items), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.DoesNotContain("sensitive", result.Error.Message);
    }

    /// <summary>Другой владелец или диалог отклоняется до провайдера, даже если его canonical input корректен.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnerAndDialogGuardsRunBeforeProviders(bool ownerMismatch)
    {
        Provider provider = new((_, _) => throw new InvalidOperationException("Провайдер не должен вызываться."));
        ApplicationCallContext call = ownerMismatch
            ? new(DIALOG_ID, DialogOwnerId.From("Tenant/user"), Guid.NewGuid(), "agent")
            : new(DialogId.From(Guid.NewGuid()), OWNER, Guid.NewGuid(), "agent");

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(call, Snapshot(), Request(), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ownerMismatch ? ServiceErrorType.Forbidden : ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, provider.Calls);
    }

    /// <summary>Точное равенство сроку запрещает подготовку, а предыдущий UTC tick ещё допустим.</summary>
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task FixedExpiryBoundaryDoesNotMove(long ticks, bool success)
    {
        Provider provider = new((_, _) => Success([]));
        DialogSnapshot dialog = Snapshot();

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(Call(), dialog, Request(), dialog.ExpiresAtUtc!.Value.AddTicks(ticks), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(success, result.Success);
        Assert.Equal(success ? 1 : 0, provider.Calls);
        Assert.Equal(NOW_UTC.AddDays(1), dialog.ExpiresAtUtc);
        if (!success)
        {
            Assert.Null(result.Data);
            Assert.Equal(ServiceErrorType.Expired, result.Error!.Type);
        }
    }

    /// <summary>Покрытие дыр/неокончившегося turn и непринятый compact явно отклоняются до providers.</summary>
    [Theory]
    [InlineData("gap")]
    [InlineData("reordered")]
    [InlineData("duplicate-sequence")]
    [InlineData("duplicate-id")]
    [InlineData("above-history")]
    [InlineData("in-progress-prefix")]
    [InlineData("incomplete-compact")]
    [InlineData("failed-compact")]
    [InlineData("canceled-compact")]
    public async Task InvalidHistoryOrCoverageIsNotSilentlyRepaired(string shape)
    {
        StoredDialogTurn one = Turn(1, DialogTurnStatus.Completed, [Message("one")]);
        StoredDialogTurn two = Turn(2, DialogTurnStatus.Completed, [Message("two")]);
        StoredDialogTurn[] turns = shape switch
        {
            "gap" => [two],
            "reordered" => [two, one],
            "duplicate-sequence" => [one, Turn(1, DialogTurnStatus.Completed, [])],
            "duplicate-id" => [one, new(one.Id, 2, DialogTurnStatus.Completed, [], [])],
            "in-progress-prefix" => [Turn(1, DialogTurnStatus.InProgress, [])],
            _ => [one]
        };
        ModelResponse compact = shape switch
        {
            "incomplete-compact" => ModelResponse.Incomplete([]),
            "failed-compact" => ModelResponse.Failed([], new(ServiceErrorType.Rejected, "Отказ модели.")),
            "canceled-compact" => ModelResponse.Canceled([]),
            _ => ModelResponse.Completed([])
        };
        StoredDialogContext context = new(1, shape == "above-history" ? 2 : 1, compact);
        Provider provider = new((_, _) => Success([]));

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(Call(), Snapshot(turns, context), Request(), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(turns.Select(turn => turn.Sequence), Snapshot(turns, context).Turns.Select(turn => turn.Sequence));
    }

    /// <summary>Actual agent/turn/owner передаются провайдерам; никакая persisted agent ownership не придумывается.</summary>
    [Fact]
    public async Task ApplicationSelectsProvidersAndEachReceivesActualAgentAndOnlyNewInput()
    {
        Provider selected = new((_, _) => Success([]));
        Provider unrelated = new((_, _) => throw new InvalidOperationException("Не выбран приложением."));
        ApplicationCallContext call = Call("second-agent");
        CanonicalModelItem fresh = Message("new");

        ServiceResult<ModelRequest> result = await new ContextBuilder([selected]).BuildAsync(call,
            Snapshot([Turn(1, DialogTurnStatus.Completed, [Message("private-history")])]), Request([fresh]), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(call, selected.Request!.Call);
        Assert.Equal(call.TurnId, selected.Request.Call.TurnId);
        Assert.Same(fresh, Assert.Single(selected.Request.NewInput));
        Assert.Equal(0, unrelated.Calls);
    }

    /// <summary>Ожидаемый отказ после первого вклада передаётся тем же объектом, без частичного запроса/fallback.</summary>
    [Fact]
    public async Task ProviderFailureStopsSequenceAndKeepsSameErrorEvenAfterLateCancellation()
    {
        using CancellationTokenSource source = new();
        ServiceError expected = new(ServiceErrorType.Forbidden, "Разрешённые данные недоступны.");
        Provider first = new((_, _) => Success([Message("first")]));
        Provider denied = new((_, _) => { source.Cancel(); return Task.FromResult(ServiceResult<ContextContribution>.Fail(expected)); });
        Provider third = new((_, _) => Success([]));

        ServiceResult<ModelRequest> result = await new ContextBuilder([first, denied, third]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC, source.Token);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Same(expected, result.Error);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, denied.Calls);
        Assert.Equal(0, third.Calls);
    }

    /// <summary>Неожиданное исключение поставщика распространяется неизменным и останавливает последовательность.</summary>
    [Fact]
    public async Task UnexpectedProviderExceptionIsNotMasked()
    {
        InvalidOperationException expected = new("synthetic-private-detail");
        Provider failed = new((_, _) => throw expected);
        Provider next = new((_, _) => Success([]));

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ContextBuilder([failed, next]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(0, next.Calls);
    }

    /// <summary>Отмена до начала или после успешного провайдера не возвращает подготовленный запрос.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerCancellationUsesOriginalTokenBeforeAndAfterProvider(bool before)
    {
        using CancellationTokenSource source = new();
        Provider provider = new((_, token) => { Assert.Equal(source.Token, token); source.Cancel(); return Success([]); });
        Provider next = new((_, _) => Success([]));
        if (before)
        {
            source.Cancel();
        }

        OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ContextBuilder([provider, next]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC, source.Token));

        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(before ? 0 : 1, provider.Calls);
        Assert.Equal(0, next.Calls);
    }

    /// <summary>Провайдеры ожидаются последовательно; отмена не оставляет отсоединённых задач или дальнейших вызовов.</summary>
    [Fact]
    public async Task ProvidersAreAwaitedSequentiallyAndCanceledTaskIsObserved()
    {
        using CancellationTokenSource source = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider first = new(async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return ServiceResult<ContextContribution>.Ok(new([]));
        });
        Provider next = new((_, _) => Success([]));
        Task<ServiceResult<ModelRequest>> task = new ContextBuilder([first, next]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC, source.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(task.IsCompleted);
            Assert.Equal(0, next.Calls);
            source.Cancel();
            OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.Equal(source.Token, failure.CancellationToken);
            Assert.Equal(0, next.Calls);
        }
        finally
        {
            source.Cancel();
            try { await task; }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        }
    }

    /// <summary>Выбранная коллекция провайдеров фиксируется; пустой вклад допустим и не меняет источники.</summary>
    [Fact]
    public async Task ProviderSelectionAndAllSourceCollectionsRemainIndependent()
    {
        Provider first = new((_, _) => Success([]));
        List<IContextProvider> providers = [first];
        ContextBuilder builder = new(providers);
        providers.Clear();
        List<CanonicalModelItem> input = [Message("new")];
        ModelRequest request = Request(input);
        input.Clear();

        ServiceResult<ModelRequest> result = await builder.BuildAsync(Call(), Snapshot(), request, NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, first.Calls);
        Assert.Single(request.Input);
        Assert.Single(result.Data!.Input);
        Assert.Throws<NotSupportedException>(() => ((IList<CanonicalModelItem>)result.Data.Input).Clear());
    }

    /// <summary>Повторный call_id разрешён для отдельных пар между обращениями и источниками контекста.</summary>
    [Theory]
    [InlineData("turns")]
    [InlineData("provider-tail")]
    [InlineData("window-new")]
    [InlineData("pending")]
    public async Task ReusedCallIdMatchesEveryPrecedingUnclosedCall(string boundary)
    {
        CanonicalModelItem call = FunctionCall("reused");
        CanonicalModelItem output = FunctionOutput("reused");
        CanonicalModelItem[] pair = [call, output];
        CanonicalModelItem[] providerItems = boundary == "provider-tail" ? pair : [];
        StoredDialogContext? context = boundary == "window-new" ? new(1, 0, ModelResponse.Completed(pair)) : null;
        StoredDialogTurn[] turns = boundary switch
        {
            "turns" => [Turn(1, DialogTurnStatus.Completed, pair), Turn(2, DialogTurnStatus.InProgress, pair)],
            "provider-tail" => [Turn(1, DialogTurnStatus.Completed, pair)],
            "pending" => [Turn(1, DialogTurnStatus.InProgress, [call, call, output, output])],
            _ => []
        };
        Provider provider = new((_, _) => Success(providerItems));

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(Call(), Snapshot(turns, context),
            Request(boundary == "window-new" ? pair : []), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(boundary == "pending" ? new[] { call, call, output, output } : new[] { call, output, call, output }, result.Data!.Input);
    }

    /// <summary>Один output не закрывает два известных call с одним ID.</summary>
    [Fact]
    public async Task RepeatedPendingCallsNeedSeparateResults()
    {
        ServiceResult<ModelRequest> result = await new ContextBuilder([]).BuildAsync(Call(), Snapshot(),
            Request([FunctionCall("reused"), FunctionCall("reused"), FunctionOutput("reused")]), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
    }

    /// <summary>Сборщик требует явное UTC и не вычисляет время или продление срока из конфигурации.</summary>
    [Fact]
    public async Task NonUtcTimeIsRejectedBeforeProvider()
    {
        Provider provider = new((_, _) => Success([]));
        await Assert.ThrowsAsync<ArgumentException>(() => new ContextBuilder([provider]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC.ToOffset(TimeSpan.FromHours(7)), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.Calls);
    }

    /// <summary>Каждая роль разрешённого провайдером сообщения передаётся без ограничения и повышения.</summary>
    [Theory]
    [InlineData("user")]
    [InlineData("system")]
    [InlineData("developer")]
    [InlineData("assistant")]
    [InlineData("tool")]
    [InlineData("future-role")]
    public async Task AuthorizedProviderRolesRemainExactlyAsSupplied(string role)
    {
        CanonicalModelItem item = Item(JsonSerializer.Serialize(new { type = "message", role, content = "business-data" }));
        Provider provider = new((_, _) => Success([item]));

        ServiceResult<ModelRequest> result = await new ContextBuilder([provider]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(item, Assert.Single(result.Data!.Input));
        Assert.Equal(role, result.Data.Input[0].Content.GetProperty("role").GetString());
        Assert.Equal("instructions", result.Data.Instructions);
    }

    /// <summary>Terminal Incomplete относится к допустимому prefix, хотя не означает успех модели.</summary>
    [Fact]
    public async Task TerminalIncompleteTurnMayBeCoveredWithoutClaimingModelSuccess()
    {
        StoredDialogTurn turn = Turn(1, DialogTurnStatus.Incomplete, [Message("partial")]);
        CanonicalModelItem compact = Item("""{"type":"compaction","encrypted_content":"opaque"}""");
        ServiceResult<ModelRequest> result = await new ContextBuilder([]).BuildAsync(Call(),
            Snapshot([turn], new(1, 1, ModelResponse.Completed([compact]))), Request(), NOW_UTC, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Same(compact, Assert.Single(result.Data!.Input));
        Assert.Equal(DialogTurnStatus.Incomplete, turn.Status);
    }

    /// <summary>Успешный второй провайдер начинается только после окончания первого; task всегда наблюдается.</summary>
    [Fact]
    public async Task SuccessfulProviderMustFinishBeforeNextProviderStarts()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool firstFinished = false;
        Provider first = new(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            firstFinished = true;
            return ServiceResult<ContextContribution>.Ok(new([Message("first")]));
        });
        Provider next = new((_, _) => { Assert.True(firstFinished); return Success([Message("next")]); });
        using CancellationTokenSource source = new();
        Task<ServiceResult<ModelRequest>> task = new ContextBuilder([first, next]).BuildAsync(Call(), Snapshot(), Request(), NOW_UTC, source.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(task.IsCompleted);
            Assert.Equal(0, next.Calls);
            release.TrySetResult();
            ServiceResult<ModelRequest> result = await task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Success);
            Assert.Equal(new[] { "first", "next" }, result.Data!.Input.Select(item => item.Content.GetProperty("content").GetString()));
            Assert.Equal(1, next.Calls);
        }
        finally
        {
            source.Cancel();
            release.TrySetResult();
            try { await task; }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        }
    }

    /// <summary>Создаёт вызов без секретов и с выбранным приложением агентом.</summary>
    private static ApplicationCallContext Call(string agentId = "agent") => new(DIALOG_ID, OWNER, Guid.NewGuid(), agentId);

    /// <summary>Создаёт неизменяемое состояние чтения с фиксированным сроком.</summary>
    private static DialogSnapshot Snapshot(IEnumerable<StoredDialogTurn>? turns = null, StoredDialogContext? context = null) =>
        new(new(DIALOG_ID, Guid.NewGuid(), 10), OWNER, NOW_UTC.AddDays(-1), NOW_UTC.AddDays(1), 100, turns ?? [], context);

    /// <summary>Создаёт сохранённый turn без результатов отдельных шагов.</summary>
    private static StoredDialogTurn Turn(long sequence, DialogTurnStatus status, IEnumerable<CanonicalModelItem> items) =>
        new(Guid.NewGuid(), sequence, status, items, []);

    /// <summary>Создаёт новый ещё не сохранённый вход для публичного API.</summary>
    private static ModelRequest Request(IEnumerable<CanonicalModelItem>? input = null) => new("model", "medium", "instructions", input ?? [], []);

    /// <summary>Возвращает полный допустимый вклад провайдера.</summary>
    private static Task<ServiceResult<ContextContribution>> Success(IEnumerable<CanonicalModelItem> items) =>
        Task.FromResult(ServiceResult<ContextContribution>.Ok(new(items)));

    /// <summary>Фиксирует канонический объект до уничтожения исходного документа.</summary>
    private static CanonicalModelItem Item(string json) => new(Json(json));

    /// <summary>Фиксирует независимое JSON-значение тестовых данных.</summary>
    private static JsonElement Json(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Создаёт сообщение с явно сохранённой ролью.</summary>
    private static CanonicalModelItem Message(string text) => Item(JsonSerializer.Serialize(new { type = "message", role = "user", content = text }));

    /// <summary>Создаёт полный канонический вызов без локального разбора arguments.</summary>
    private static CanonicalModelItem FunctionCall(string id) => Item(JsonSerializer.Serialize(new
    {
        type = "function_call", call_id = id, name = "GetOrder", arguments = "{\"orderId\":42}", future = new { opaque = "retain" }
    }));

    /// <summary>Создаёт канонический результат с неизвестным расширением.</summary>
    private static CanonicalModelItem FunctionOutput(string id) => Item(JsonSerializer.Serialize(new
    {
        type = "function_call_output", call_id = id, output = "{\"ok\":true}", future = new { values = new[] { 1, 2 } }
    }));

    /// <inheritdoc/>
    private class Provider(Func<ContextRequest, CancellationToken, Task<ServiceResult<ContextContribution>>> run) : IContextProvider
    {
        public int Calls { get; private set; }
        public ContextRequest? Request { get; private set; }

        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Request = request;
            return run(request, cancellationToken);
        }
    }
}
