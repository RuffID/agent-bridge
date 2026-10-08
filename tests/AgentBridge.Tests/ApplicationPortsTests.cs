using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Прямые проверки production DTO, immutable snapshots, JSON lifetime и ServiceResult.</summary>
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
    public void ModelLifecycleIsIndependentFromNonemptyOutput(ModelResponseStatus status)
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
        ServiceResult<ModelResponse> result = ServiceResult<ModelResponse>.Ok(response);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Same(response, result.Data);
        Assert.Equal(status, response.Status);
        Assert.Equal("partial", Assert.Single(response.Output).Content.GetProperty("text").GetString());
        Assert.Equal(status == ModelResponseStatus.Failed ? error : null, response.Error);
    }

    /// <summary>Ожидаемый отказ ServiceResult сохраняет исходную ошибку без данных.</summary>
    [Fact]
    public void ExpectedFailurePreservesErrorWithoutData()
    {
        ServiceError forbidden = new(ServiceErrorType.Forbidden, "Доступ запрещён.");
        ServiceResult<ContextContribution> context = ServiceResult<ContextContribution>.Fail(forbidden);
        ServiceResult<ToolOutput> output = ServiceResult<ToolOutput>.Fail(forbidden);

        Assert.False(context.Success);
        Assert.False(output.Success);
        Assert.Null(context.Data);
        Assert.Null(output.Data);
        Assert.Same(forbidden, context.Error);
        Assert.Same(forbidden, output.Error);
        Assert.Same(forbidden, ServiceResult<ModelResponse>.Fail(output.Error!).Error);
        ServiceError timeout = new(ServiceErrorType.Timeout, "Истёк бюджет времени.");
        Assert.Same(timeout, ModelResponse.Failed([], timeout).Error);
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

    /// <summary>Неизвестный полный бюджет не превращается в нулевой; estimate не может быть меньше KnownTokens.</summary>
    [Fact]
    public void TokenCountRetainsKnownAndUnknownBudget()
    {
        ContextTokenCount count = new("known-encoding", 100, null, true);

        Assert.True(count.HasOpaqueContent);
        Assert.Null(count.EstimatedInputTokens);
        Assert.Equal(100, count.KnownTokens);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContextTokenCount("known", 100, 99, true));
    }

    /// <summary>Доступ раскрывает ключ только явно, без ToString или обычной сериализации.</summary>
    [Fact]
    public void ModelAccessIsSafeToRepresent()
    {
        ModelAccess access = new("synthetic-user-secret");

        Assert.Equal("synthetic-user-secret", access.RevealApiKey());
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

    /// <summary>Снимок считает equality истечением срока, не продлевая фиксированный expiry.</summary>
    [Fact]
    public void SnapshotExpiryIncludesExactBoundary()
    {
        DialogSnapshot snapshot = new(Token(), OWNER, NOW_UTC, NOW_UTC.AddDays(1), 0, [], null);

        Assert.True(snapshot.IsExpired(snapshot.ExpiresAtUtc!.Value));
        Assert.False(snapshot.IsExpired(snapshot.ExpiresAtUtc!.Value.AddTicks(-1)));
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
    /// <summary>Создаёт идентичности приложения.</summary>
    private static ApplicationCallContext Call() => new(DialogId.From(Guid.NewGuid()), OWNER, Guid.NewGuid(), "agent");
    /// <summary>Создаёт сохраняемое условие без использования Domain snapshot.</summary>
    private static DialogWriteToken Token() => new(DialogId.From(Guid.NewGuid()), Guid.NewGuid(), 0);

}
