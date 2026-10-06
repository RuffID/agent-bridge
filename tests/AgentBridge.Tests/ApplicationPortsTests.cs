using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Изолированные проверки семантики контрактов и их замены заглушками без инфраструктуры.</summary>
public class ApplicationPortsTests
{
    private static readonly DateTimeOffset NOW_UTC = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From("tenant/user");

    /// <summary>Canonical items, envelope и continuation переживают уничтожение документа и сохраняют неизвестные поля.</summary>
    [Fact]
    public void CanonicalSnapshotsOwnOpaqueAndUnknownData()
    {
        CanonicalModelItem item;
        CanonicalModelEnvelope envelope;
        ModelContinuation continuation;
        using (JsonDocument document = JsonDocument.Parse("""
            {"id":"response-42","object":"response.compaction","usage":{"input_tokens":99},
             "future":{"nested":[null,true,42]},"output":[{"type":"compaction","encrypted_content":"opaque+/==","future":{"x":1}}]}
            """))
        {
            item = new(document.RootElement.GetProperty("output")[0]);
            envelope = new(document.RootElement);
            continuation = new(document.RootElement.GetProperty("future"));
        }
        Assert.Equal("opaque+/==", item.Content.GetProperty("encrypted_content").GetString());
        Assert.Equal(1, item.Content.GetProperty("future").GetProperty("x").GetInt32());
        Assert.Equal("response-42", envelope.Content.GetProperty("id").GetString());
        Assert.Equal(99, envelope.Content.GetProperty("usage").GetProperty("input_tokens").GetInt32());
        Assert.Equal(JsonValueKind.Null, continuation.Content.GetProperty("nested")[0].ValueKind);
    }

    /// <summary>Изменение исходных коллекций не меняет запрос, ответ, историю или описания инструментов.</summary>
    [Fact]
    public void MutableSourcesCannotChangeContractSnapshots()
    {
        CanonicalModelItem item = Item("{\"type\":\"message\",\"text\":\"original\"}");
        List<CanonicalModelItem> input = [item];
        List<ModelToolDefinition> tools = [Definition()];
        ModelRequest request = new("model", "medium", "instructions", input, tools);
        ModelResponse response = ModelResponse.Incomplete(input);
        List<StoredModelStep> steps = [new(Guid.NewGuid(), response)];
        StoredDialogTurn turn = new(Guid.NewGuid(), 1, DialogTurnStatus.InProgress, input, steps);
        List<StoredDialogTurn> turns = [turn];
        DialogSnapshot snapshot = new(Token(), OWNER, NOW_UTC, NOW_UTC.AddDays(1), 10, turns, null);
        input.Clear();
        tools.Clear();
        steps.Clear();
        turns.Clear();
        Assert.Single(request.Input);
        Assert.Single(request.Tools);
        Assert.Single(response.Output);
        Assert.Single(snapshot.Turns);
        Assert.Single(turn.Items);
        Assert.Single(turn.ModelSteps);
        Assert.Throws<NotSupportedException>(() => ((IList<CanonicalModelItem>)request.Input).Clear());
    }

    /// <summary>Полученный текст при обрыве, отказе или отмене не становится Completed и не теряется.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Completed)]
    [InlineData(ModelResponseStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Failed)]
    [InlineData(ModelResponseStatus.Canceled)]
    public async Task ModelLifecycleIsIndependentFromNonemptyOutput(ModelResponseStatus status)
    {
        CanonicalModelItem[] output = [Item("{\"type\":\"message\",\"text\":\"partial\"}")];
        ServiceError error = new(ServiceErrorType.Rejected, "Модель отклонила запрос.");
        ModelResponse response = status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed(output),
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete(output),
            ModelResponseStatus.Failed => ModelResponse.Failed(output, error),
            ModelResponseStatus.Canceled => ModelResponse.Canceled(output),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        IModelGateway gateway = new FakeGateway(response);
        List<string?> deltas = [];
        ServiceResult<ModelResponse> result = await gateway.GenerateAsync(Call(), Request(), new("synthetic-user-key"),
            (update, _) => { deltas.Add(update.TextDelta); return ValueTask.CompletedTask; }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal(status, result.Data.Status);
        Assert.Equal("partial", Assert.Single(result.Data.Output).Content.GetProperty("text").GetString());
        Assert.Equal(["preview"], deltas);
        Assert.Equal(status == ModelResponseStatus.Failed ? error : null, result.Data.Error);
    }

    /// <summary>Caller cancellation до отчёта остаётся исключением с исходным токеном, а timeout имеет отдельный смысл.</summary>
    [Fact]
    public async Task CallerCancellationIsNotTimeoutOrCompleted()
    {
        using CancellationTokenSource source = new();
        source.Cancel();
        IModelGateway gateway = new FakeGateway(ModelResponse.Completed([]));
        OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.GenerateAsync(Call(), Request(), new("synthetic"), cancellationToken: source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
        ServiceError timeout = new(ServiceErrorType.Timeout, "Истёк бюджет времени.");
        Assert.Equal(ServiceErrorType.Timeout, ModelResponse.Failed([], timeout).Error?.Type);
    }

    /// <summary>Неожиданная ошибка callback не скрывается успехом и не оставляет фоновое продолжение.</summary>
    [Fact]
    public async Task UnexpectedCallbackFailurePropagatesWithoutCompletion()
    {
        IModelGateway gateway = new FakeGateway(ModelResponse.Completed([]));
        InvalidOperationException expected = new("Ошибка приложения.");
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.GenerateAsync(Call(), Request(), new("synthetic"), (_, _) => throw expected, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
    }

    /// <summary>Ошибка приложения передаётся целиком, не содержит данные и не заменяется выдуманным tool output.</summary>
    [Fact]
    public async Task ContextAndToolPortsPropagateExpectedFailureWithoutData()
    {
        ServiceError forbidden = new(ServiceErrorType.Forbidden, "Доступ запрещён.");
        IContextProvider provider = new DeniedContextProvider(forbidden);
        IToolHandler tool = new DeniedToolHandler(forbidden);
        ServiceResult<ContextContribution> context = await provider.GetContextAsync(new(Call(), []), cancellationToken: TestContext.Current.CancellationToken);
        using JsonDocument arguments = JsonDocument.Parse("{\"orderId\":42}");
        ServiceResult<ToolOutput> output = await tool.ExecuteAsync(new(Call(), "call-42", tool.Definition.Name, arguments.RootElement), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(context.Success);
        Assert.False(output.Success);
        Assert.Null(context.Data);
        Assert.Null(output.Data);
        Assert.Same(forbidden, context.Error);
        Assert.Same(forbidden, output.Error);
        Assert.Same(forbidden, ServiceResult<ModelResponse>.Fail(output.Error!).Error);
    }

    /// <summary>Аргументы, схема и результат инструмента не зависят от срока жизни исходного JSON.</summary>
    [Fact]
    public void ToolDataOwnsSchemaArgumentsAndNullableJsonOutput()
    {
        ToolInvocation invocation;
        ModelToolDefinition definition;
        ToolOutput output;
        using (JsonDocument arguments = JsonDocument.Parse("{\"future\":{\"nested\":[1,2]}}"))
        using (JsonDocument schema = JsonDocument.Parse("{\"type\":\"object\",\"future\":true}"))
        using (JsonDocument value = JsonDocument.Parse("null"))
        {
            invocation = new(Call(), "call-1", "GetOrder", arguments.RootElement);
            definition = new("GetOrder", "Получить заказ", schema.RootElement, true);
            output = new(value.RootElement);
        }
        Assert.Equal(2, invocation.Arguments.GetProperty("future").GetProperty("nested")[1].GetInt32());
        Assert.True(definition.Parameters.GetProperty("future").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ServiceResult<ToolOutput>.Ok(output).Data!.Content.ValueKind);
    }

    /// <summary>Счётчик получает весь подготовленный запрос; opaque не объявляется известной локальной суммой.</summary>
    [Fact]
    public async Task TokenPortReceivesInstructionsToolsAndOpaqueInput()
    {
        ModelRequest request = Request();
        CapturingTokenCounter counter = new();
        ServiceResult<ContextTokenCount> result = await counter.CountAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(request, counter.Received);
        Assert.Equal("instructions", counter.Received!.Instructions);
        Assert.Single(counter.Received.Tools);
        Assert.Equal("opaque", Assert.Single(counter.Received.Input).Content.GetProperty("encrypted_content").GetString());
        Assert.NotNull(result.Data);
        Assert.True(result.Data.HasOpaqueContent);
        Assert.Null(result.Data.EstimatedInputTokens);
        Assert.Equal(100, result.Data.KnownTokens);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContextTokenCount("known", 100, 99, true));
    }

    /// <summary>Выбранный ключ передаётся на вызов и не раскрывается ToString или обычной сериализацией.</summary>
    [Fact]
    public async Task SelectedAccessIsPerCallAndSafeToRepresent()
    {
        FakeGateway gateway = new(ModelResponse.Completed([]));
        ModelAccess access = new("synthetic-user-secret");
        await gateway.GenerateAsync(Call(), Request(), access, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(access, gateway.ReceivedAccess);
        Assert.Equal("synthetic-user-secret", gateway.ReceivedAccess!.RevealApiKey());
        Assert.DoesNotContain("synthetic-user-secret", access.ToString());
        Assert.DoesNotContain("synthetic-user-secret", JsonSerializer.Serialize(access));
    }

    /// <summary>Успешный generic результат не допускает null; ожидаемый отказ не несёт данные.</summary>
    [Fact]
    public void ResultFactoriesRejectAmbiguousStates()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceResult<ModelResponse>.Ok(null!));
        Assert.Throws<ArgumentNullException>(() => ServiceResult.Fail(null!));
        Assert.Throws<ArgumentNullException>(() => ModelResponse.Failed([], null!));
        Assert.Throws<ArgumentException>(() => new CanonicalModelItem(default));
        Assert.Throws<ArgumentException>(() => new ToolOutput(default));
        Assert.Throws<ArgumentException>(() => new DialogAccess(Token().DialogId, OWNER, NOW_UTC.ToOffset(TimeSpan.FromHours(1))));
    }

    /// <summary>Порт может атомарно отказать позднему результату без изменения данных по каждому обязательному условию.</summary>
    [Theory]
    [InlineData("missing", ServiceErrorType.NotFound)]
    [InlineData("owner", ServiceErrorType.Forbidden)]
    [InlineData("expiry", ServiceErrorType.Expired)]
    [InlineData("incarnation", ServiceErrorType.Conflict)]
    [InlineData("revision", ServiceErrorType.Conflict)]
    [InlineData("dialog", ServiceErrorType.Conflict)]
    public async Task StorageContractExpressesAllLateWriteGuards(string condition, ServiceErrorType errorType)
    {
        ContractStore store = new();
        DialogWriteToken expected = store.Snapshot!.Token;
        DialogAccess access = new(expected.DialogId, OWNER, NOW_UTC);
        Guid turnId = Guid.NewGuid();
        ServiceResult<DialogWriteToken> begun = await ((IDialogTurnWriter)store).BeginAsync(access, expected, turnId, [], TestContext.Current.CancellationToken);
        expected = begun.Data!;
        DialogSnapshot before = store.Snapshot!;
        switch (condition)
        {
            case "missing": store.Snapshot = null; break;
            case "owner": access = new(expected.DialogId, DialogOwnerId.From("other"), NOW_UTC); break;
            case "expiry": access = new(expected.DialogId, OWNER, before.ExpiresAtUtc); break;
            case "incarnation": expected = new(expected.DialogId, Guid.NewGuid(), expected.Revision); break;
            case "revision": expected = new(expected.DialogId, expected.IncarnationId, expected.Revision - 1); break;
            case "dialog": access = new(DialogId.From(Guid.NewGuid()), OWNER, NOW_UTC); break;
        }
        ServiceResult<DialogWriteToken> result = await ((IDialogTurnWriter)store).FinishAsync(access, expected,
            turnId, DialogTurnStatus.Completed, [Item("{\"type\":\"message\"}")], [], cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(errorType, result.Error?.Type);
        Assert.Same(condition == "missing" ? null : before, store.Snapshot);
    }

    /// <summary>Старая жизнь не применяется к новой с тем же ID и revision; доменный object lifetime не используется.</summary>
    [Fact]
    public async Task RecreatedDialogCannotAcceptOldIncarnation()
    {
        ContractStore store = new();
        DialogWriteToken old = store.Snapshot!.Token;
        store.Snapshot = new(new(old.DialogId, Guid.NewGuid(), old.Revision), OWNER, NOW_UTC, NOW_UTC.AddDays(1), 0, [], null);
        ServiceResult<DialogWriteToken> result = await ((IDialogTurnWriter)store).BeginAsync(new(old.DialogId, OWNER, NOW_UTC), old, Guid.NewGuid(), [], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, result.Error?.Type);
        Assert.Empty(store.Snapshot.Turns);
    }

    /// <summary>Чтение срока после expiry остаётся доступным владельцу, но не разрешает новое обращение.</summary>
    [Fact]
    public async Task ExpiredMetadataCanBeReadWithoutAuthorizingNewTurn()
    {
        ContractStore store = new();
        DialogSnapshot snapshot = store.Snapshot!;
        DialogAccess access = new(snapshot.Token.DialogId, OWNER, snapshot.ExpiresAtUtc);
        ServiceResult<DialogSnapshot> read = await ((IDialogReader)store).ReadAsync(access, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(read.Success);
        Assert.True(read.Data!.IsExpired(access.NowUtc));
        Assert.False(read.Data.IsExpired(access.NowUtc.AddTicks(-1)));
        ServiceResult<DialogWriteToken> begin = await store.BeginAsync(access, snapshot.Token, Guid.NewGuid(), [], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Expired, begin.Error?.Type);
        Assert.Same(snapshot, store.Snapshot);
    }

    /// <summary>Контрактный round-trip хранит envelope отдельно от items и сохраняет связь с шагом и обращением.</summary>
    [Fact]
    public async Task StepRoundTripPreservesFullEnvelopeWithoutTurningItIntoInputItem()
    {
        ContractStore store = new();
        IDialogTurnWriter writer = store;
        IDialogReader reader = store;
        DialogWriteToken token = store.Snapshot!.Token;
        DialogAccess access = new(token.DialogId, OWNER, NOW_UTC);
        Guid turnId = Guid.NewGuid();
        Guid stepId = Guid.NewGuid();
        token = (await writer.BeginAsync(access, token, turnId, [Item("{\"type\":\"message\",\"role\":\"user\"}")], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        CanonicalModelEnvelope envelope;
        ModelContinuation continuation;
        CanonicalModelItem item;
        using (JsonDocument document = JsonDocument.Parse("""
            {"id":"response-1","usage":{"input_tokens":10},"unknown":{"extra":true},
             "output":[{"type":"reasoning","encrypted_content":"opaque"}]}
            """))
        {
            envelope = new(document.RootElement);
            continuation = new(document.RootElement.GetProperty("unknown"));
            item = new(document.RootElement.GetProperty("output")[0]);
        }
        ModelResponse response = ModelResponse.Incomplete([item], envelope, continuation);
        token = (await writer.AppendAsync(access, token, turnId, [item], [new(stepId, response)], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        token = (await writer.FinishAsync(access, token, turnId, DialogTurnStatus.Incomplete, [], [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        DialogSnapshot read = (await reader.ReadAsync(access, cancellationToken: TestContext.Current.CancellationToken)).Data!;
        StoredDialogTurn turn = Assert.Single(read.Turns);
        StoredModelStep step = Assert.Single(turn.ModelSteps);
        Assert.Equal(turnId, turn.Id);
        Assert.Equal(stepId, step.StepId);
        Assert.Equal(ModelResponseStatus.Incomplete, step.Response.Status);
        Assert.Equal("response-1", step.Response.Envelope!.Content.GetProperty("id").GetString());
        Assert.Equal(10, step.Response.Envelope.Content.GetProperty("usage").GetProperty("input_tokens").GetInt32());
        Assert.True(step.Response.Continuation!.Content.GetProperty("extra").GetBoolean());
        Assert.Equal("opaque", Assert.Single(step.Response.Output).Content.GetProperty("encrypted_content").GetString());
        Assert.Equal(2, turn.Items.Count);
        Assert.All(turn.Items, value => Assert.False(value.Content.TryGetProperty("usage", out _)));
        ServiceResult<DialogWriteToken> repeated = await writer.FinishAsync(access, token, turnId, DialogTurnStatus.Completed, [], [], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, repeated.Error?.Type);
        Assert.Equal(DialogTurnStatus.Incomplete, Assert.Single(store.Snapshot!.Turns).Status);
    }

    /// <summary>Compact snapshot отделяет полное окно от envelope и не фильтрует историю по terminal prefix.</summary>
    [Fact]
    public void CompactMetadataDoesNotDiscardItemsOrInProgressTurns()
    {
        CanonicalModelItem item = Item("{\"type\":\"compaction\",\"encrypted_content\":\"opaque\"}");
        using JsonDocument document = JsonDocument.Parse("{\"object\":\"response.compaction\",\"future\":true}");
        StoredDialogContext context = new(1, 0, ModelResponse.Completed([item], new(document.RootElement)));
        StoredDialogTurn turn = new(Guid.NewGuid(), 1, DialogTurnStatus.InProgress, [item], []);
        DialogSnapshot snapshot = new(Token(), OWNER, NOW_UTC, NOW_UTC.AddDays(1), 1, [turn], context);
        Assert.Equal(0, context.ThroughTurnSequence);
        Assert.Same(item, Assert.Single(context.Items));
        Assert.True(context.Compaction.Envelope!.Content.GetProperty("future").GetBoolean());
        Assert.Equal(DialogTurnStatus.InProgress, Assert.Single(snapshot.Turns).Status);
        Assert.Single(snapshot.Turns[0].Items);
    }

    /// <summary>Неуспешный compact не заменяет прежний контекст, даже если содержит непустой output.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Failed)]
    [InlineData(ModelResponseStatus.Canceled)]
    public async Task ContextPortCanRejectNoncompletedCompactionWithoutChangingSnapshot(ModelResponseStatus status)
    {
        ContractStore store = new();
        IDialogContextWriter writer = store;
        DialogSnapshot before = store.Snapshot!;
        CanonicalModelItem[] output = [Item("{\"type\":\"compaction\",\"encrypted_content\":\"opaque\"}")];
        ModelResponse response = status switch
        {
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete(output),
            ModelResponseStatus.Failed => ModelResponse.Failed(output, new(ServiceErrorType.Rejected, "Compact отклонён.")),
            ModelResponseStatus.Canceled => ModelResponse.Canceled(output),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        ServiceResult<DialogWriteToken> result = await writer.SaveAsync(new(before.Token.DialogId, OWNER, NOW_UTC), before.Token, 0, response, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
        Assert.Same(before, store.Snapshot);
        Assert.Null(store.Snapshot!.ActiveContext);
    }

    /// <summary>Read/write порты сохраняют полный compact envelope отдельно от канонического окна.</summary>
    [Fact]
    public async Task AcceptedCompactionRoundTripPreservesEnvelopeAndHistory()
    {
        ContractStore store = new();
        DialogWriteToken token = store.Snapshot!.Token;
        DialogAccess access = new(token.DialogId, OWNER, NOW_UTC);
        Guid turnId = Guid.NewGuid();
        token = (await store.BeginAsync(access, token, turnId, [Item("{\"type\":\"message\"}")], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        token = (await store.FinishAsync(access, token, turnId, DialogTurnStatus.Completed, [], [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        using JsonDocument document = JsonDocument.Parse("{\"object\":\"response.compaction\",\"id\":\"compact-1\",\"unknown\":true}");
        ModelResponse compact = ModelResponse.Completed([Item("{\"type\":\"compaction\",\"encrypted_content\":\"opaque\"}")], new(document.RootElement));
        IDialogContextWriter writer = store;
        ServiceResult<DialogWriteToken> saved = await writer.SaveAsync(access, token, 1, compact, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(saved.Success);
        DialogSnapshot snapshot = (await ((IDialogReader)store).ReadAsync(access, cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Single(snapshot.Turns);
        Assert.NotNull(snapshot.ActiveContext);
        Assert.Equal("compact-1", snapshot.ActiveContext.Compaction.Envelope!.Content.GetProperty("id").GetString());
        Assert.True(snapshot.ActiveContext.Compaction.Envelope.Content.GetProperty("unknown").GetBoolean());
        Assert.Equal("opaque", Assert.Single(snapshot.ActiveContext.Items).Content.GetProperty("encrypted_content").GetString());
        Assert.False(snapshot.ActiveContext.Items[0].Content.TryGetProperty("object", out _));
    }

    /// <summary>Создаёт независимый канонический элемент.</summary>
    private static CanonicalModelItem Item(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(document.RootElement);
    }
    /// <summary>Создаёт описание тестового инструмента.</summary>
    private static ModelToolDefinition Definition()
    {
        using JsonDocument schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}");
        return new("GetOrder", "Получить заказ", schema.RootElement, false);
    }
    /// <summary>Создаёт полный вход без доступа к инфраструктуре.</summary>
    private static ModelRequest Request() => new("known-model", "medium", "instructions",
        [Item("{\"type\":\"reasoning\",\"encrypted_content\":\"opaque\"}")], [Definition()]);
    /// <summary>Создаёт идентичности приложения.</summary>
    private static ApplicationCallContext Call() => new(DialogId.From(Guid.NewGuid()), OWNER, Guid.NewGuid(), "agent");
    /// <summary>Создаёт сохраняемое условие без использования Domain snapshot.</summary>
    private static DialogWriteToken Token() => new(DialogId.From(Guid.NewGuid()), Guid.NewGuid(), 0);

    /// <inheritdoc/>
    private class FakeGateway(ModelResponse response) : IModelGateway
    {
        /// <summary>Выбранный доступ последнего вызова.</summary>
        public ModelAccess? ReceivedAccess { get; private set; }
        /// <inheritdoc/>
        public async Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedAccess = access;
            if (onUpdate is not null)
            {
                await onUpdate(new("preview", null), cancellationToken);
            }
            return ServiceResult<ModelResponse>.Ok(response);
        }
        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            CancellationToken cancellationToken = default) => GenerateAsync(call, request, access, cancellationToken: cancellationToken);
    }
    /// <inheritdoc/>
    private class DeniedContextProvider(ServiceError error) : IContextProvider
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ContextContribution>.Fail(error));
    }
    /// <inheritdoc/>
    private class DeniedToolHandler(ServiceError error) : IToolHandler
    {
        /// <inheritdoc/>
        public ModelToolDefinition Definition { get; } = ApplicationPortsTests.Definition();
        /// <inheritdoc/>
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ToolOutput>.Fail(error));
    }
    /// <inheritdoc/>
    private class CapturingTokenCounter : IContextTokenCounter
    {
        /// <summary>Полный запрос, поступивший на границу.</summary>
        public ModelRequest? Received { get; private set; }
        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            Received = request;
            return Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new("known-encoding", 100, null, true)));
        }
    }
    /// <inheritdoc cref="IDialogTurnWriter"/>
    /// <remarks>Однопоточная контрактная заглушка. Не доказывает EF, restart или реальную concurrency.</remarks>
    private class ContractStore : IDialogReader, IDialogTurnWriter, IDialogContextWriter
    {
        /// <summary>Управляемый снимок заглушки; null моделирует удаление.</summary>
        public DialogSnapshot? Snapshot { get; set; } = new(Token(), OWNER, NOW_UTC, NOW_UTC.AddDays(1), 0, [], null);

        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ServiceError? error = Check(access, null, false);
            return Task.FromResult(error is null ? ServiceResult<DialogSnapshot>.Ok(Snapshot!) : ServiceResult<DialogSnapshot>.Fail(error));
        }
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> BeginAsync(DialogAccess access, DialogWriteToken expected,
            Guid turnId, IReadOnlyList<CanonicalModelItem> input, CancellationToken cancellationToken = default) =>
            Change(access, expected, turnId, DialogTurnStatus.InProgress, input, [], true, cancellationToken);
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> AppendAsync(DialogAccess access, DialogWriteToken expected,
            Guid turnId, IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps, CancellationToken cancellationToken = default) =>
            Change(access, expected, turnId, DialogTurnStatus.InProgress, items, modelSteps, false, cancellationToken);
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> FinishAsync(DialogAccess access, DialogWriteToken expected,
            Guid turnId, DialogTurnStatus status, IReadOnlyList<CanonicalModelItem> newItems, IReadOnlyList<StoredModelStep> modelSteps,
            CancellationToken cancellationToken = default)
        {
            if (status == DialogTurnStatus.InProgress || !Enum.IsDefined(status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            return Change(access, expected, turnId, status, newItems, modelSteps, false, cancellationToken);
        }
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
            long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ServiceError? error = Check(access, expected);
            if (error is not null)
            {
                return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(error));
            }
            DialogSnapshot current = Snapshot!;
            bool invalidPrefix = throughTurnSequence < (current.ActiveContext?.ThroughTurnSequence ?? 0)
                || throughTurnSequence > current.Turns.Count
                || current.Turns.Take((int)throughTurnSequence).Any(turn => turn.Status == DialogTurnStatus.InProgress);
            if (compaction.Status != ModelResponseStatus.Completed || invalidPrefix)
            {
                return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new(ServiceErrorType.Validation, "Compact не может быть принят.")));
            }
            DialogWriteToken token = new(current.Token.DialogId, current.Token.IncarnationId, current.Token.Revision + 1);
            StoredDialogContext context = new((current.ActiveContext?.Version ?? 0) + 1, throughTurnSequence, compaction);
            Snapshot = new(token, current.OwnerId, current.CreatedAtUtc, current.ExpiresAtUtc, current.ContentBytes, current.Turns, context);
            return Task.FromResult(ServiceResult<DialogWriteToken>.Ok(token));
        }
        /// <summary>Моделирует единую проверку и замену снимка без I/O.</summary>
        private Task<ServiceResult<DialogWriteToken>> Change(DialogAccess access, DialogWriteToken expected, Guid turnId,
            DialogTurnStatus status, IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> steps, bool begin, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ServiceError? error = Check(access, expected);
            if (error is not null)
            {
                return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(error));
            }
            DialogSnapshot current = Snapshot!;
            StoredDialogTurn? existing = current.Turns.SingleOrDefault(turn => turn.Id == turnId);
            if (begin ? existing is not null : existing is null || existing.Status != DialogTurnStatus.InProgress)
            {
                return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new(ServiceErrorType.Conflict, "Обращение неактуально.")));
            }
            StoredDialogTurn changed = new(turnId, existing?.Sequence ?? current.Turns.Count + 1, status,
                (existing?.Items ?? []).Concat(items), (existing?.ModelSteps ?? []).Concat(steps));
            List<StoredDialogTurn> turns = current.Turns.Where(turn => turn.Id != turnId).Append(changed).OrderBy(turn => turn.Sequence).ToList();
            DialogWriteToken token = new(current.Token.DialogId, current.Token.IncarnationId, current.Token.Revision + 1);
            Snapshot = new(token, current.OwnerId, current.CreatedAtUtc, current.ExpiresAtUtc, current.ContentBytes, turns, current.ActiveContext);
            return Task.FromResult(ServiceResult<DialogWriteToken>.Ok(token));
        }
        /// <summary>Все обязательные условия доступны через чистые контракты.</summary>
        private ServiceError? Check(DialogAccess access, DialogWriteToken? expected, bool requireAvailable = true)
        {
            if (Snapshot is null)
            {
                return new(ServiceErrorType.NotFound, "Диалог отсутствует.");
            }
            if (!Snapshot.Token.DialogId.Equals(access.DialogId) || expected is not null && !expected.DialogId.Equals(access.DialogId))
            {
                return new(ServiceErrorType.Conflict, "Идентичность не совпадает.");
            }
            if (!Snapshot.OwnerId.Equals(access.OwnerId))
            {
                return new(ServiceErrorType.Forbidden, "Другой владелец.");
            }
            if (requireAvailable && access.NowUtc >= Snapshot.ExpiresAtUtc)
            {
                return new(ServiceErrorType.Expired, "Срок истёк.");
            }
            if (expected is not null && (Snapshot.Token.IncarnationId != expected.IncarnationId || Snapshot.Token.Revision != expected.Revision))
            {
                return new(ServiceErrorType.Conflict, "Устаревшее условие записи.");
            }
            return null;
        }
    }
}
