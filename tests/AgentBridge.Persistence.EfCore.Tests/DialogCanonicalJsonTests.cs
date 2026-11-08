using System.Text.Json;
using System.Text.Json.Nodes;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Регрессии сохранения и recovery JSON после сериализации через actual public ports; без БД и HTTP.</summary>
public class DialogCanonicalJsonTests
{
    private const string ASSISTANT_JSON = """
        {
          "type": "message", "role": "assistant",
          "content": [ { "type": "output_text", "text": "Привет 😀" } ],
          "unknown": { "label": "Тест", "ordered": [1, 2] }
        }
        """;
    private const string CALL_JSON = """
        {
          "type": "function_call", "call_id": "call-1", "name": "action",
          "arguments": "{}",
          "unknown": { "label": "Тест 😀", "ordered": [1, 2] }
        }
        """;

    /// <summary>Actual runner принимает raw JSON, сохраняет отчёт и продолжает после fresh storage instance без replay.</summary>
    [Fact]
    public async Task RunnerSavesFormattedUnicodeAndContinuesFromReloadedHistory()
    {
        // Arrange
        await using CatalogRunnerFixture first = new();
        DialogCatalogState created = await first.Storage.CreateAsync();
        CanonicalModelItem answer = Item(ASSISTANT_JSON);
        first.Responses.Enqueue(ModelResponse.Completed([answer]));

        // Act
        AgentRunResult completed = await first.RunAsync(created.Token, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Completed, completed.Status);
        Assert.True(completed.TerminalSaved, completed.Error?.Message);
        Assert.Equal(0, first.HandlerCalls);
        Assert.Single(first.Requests);
        DialogSnapshot stored = await first.Storage.SnapshotAsync(completed.Token!);
        StoredModelStep step = Assert.Single(Assert.Single(stored.Turns).ModelSteps);
        CanonicalModelItem restored = Assert.Single(step.Response.Output);
        Assert.NotEqual(answer.Content.GetRawText(), restored.Content.GetRawText());
        Assert.True(JsonElement.DeepEquals(answer.Content, restored.Content));
        Assert.Equal(ASSISTANT_JSON, stored.Turns[0].Items.Last().Content.GetRawText());
        Assert.Equal("Привет 😀", stored.Catalog!.Text.Snippet);

        // Arrange
        await using CatalogRunnerFixture next = new(new CatalogWriteFixture(first.Storage.Committed));
        next.Responses.Enqueue(ModelResponse.Completed([Item(ASSISTANT_JSON)]));

        // Act
        AgentRunResult continued = await next.RunAsync(stored.Token, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.True(continued.TerminalSaved, continued.Error?.Message);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Contains(Assert.Single(next.Requests).Input,
            item => JsonElement.DeepEquals(answer.Content, item.Content));
        Assert.Equal(2, (await next.Storage.SnapshotAsync(continued.Token!)).Turns.Count);
    }

    /// <summary>Разные значения, неизвестные поля, массивы и порядок output не принимаются за форматирование.</summary>
    [Theory]
    [InlineData("text")]
    [InlineData("unknown")]
    [InlineData("array")]
    [InlineData("missing-item")]
    [InlineData("reordered-items")]
    public async Task AppendRejectsContentOrPositionMismatchWithoutPartialCommit(string change)
    {
        // Arrange
        CatalogWriteFixture fixture = new();
        DialogRunState run = await fixture.BeginAsync((await fixture.CreateAsync()).Token);
        CanonicalModelItem answer = Item(ASSISTANT_JSON);
        CanonicalModelItem reasoning = Item("""{"type":"reasoning","unknown":"не показывать"}""");
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([answer, reasoning]));
        CanonicalModelItem[] items = ChangedItems(answer, reasoning, change);
        string before = JsonSerializer.Serialize(fixture.Committed);

        // Act
        ServiceResult<DialogWriteToken> result = await fixture.Writer.AppendAsync(
            new DialogRunWriteAccess(fixture.Access(run.Token), run.Lease), run.Token, run.Lease.TurnId,
            items, [step], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal("catalog_step_items_mismatch", result.Error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Committed));
        Assert.Empty(fixture.Committed.Steps);
    }

    /// <summary>Recovery сопоставляет полный JSON original позиции после round-trip, без изменения originals и replay handler.</summary>
    [Fact]
    public async Task RecoveryAcceptsEquivalentJsonAndPreservesOriginalsWithoutReplay()
    {
        // Arrange
        await using CatalogRunnerFixture fixture = new();
        DialogContinuationState pending = await SaveUnknownCallAsync(fixture);
        string before = OriginalHistory(fixture.Storage);
        DialogRecoveryRequest request = RecoveryRequest(fixture.Storage, pending);

        // Act
        ServiceResult<DialogRecoveryResult> result = await fixture.Storage.Recovery.RecoverAsync(
            request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(DialogReadiness.Ready, result.Data!.Continuation.Readiness);
        Assert.Equal(before, OriginalHistory(fixture.Storage));
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Single(fixture.Requests);

        // Arrange
        CatalogWriteFixture restarted = new(fixture.Storage.Committed);
        restarted.Reload();

        // Act
        ServiceResult<DialogSnapshot> read = await restarted.Reader.ReadCatalogAsync(
            restarted.FullAccess(result.Data.Continuation.Token), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(read.Success, read.Error?.Message);
        DialogSnapshot snapshot = read.Data!;
        Assert.Equal(DialogReadiness.Ready, snapshot.Continuation!.Readiness);
        Assert.Empty(snapshot.Continuation.Pending);
        CanonicalModelItem original = Assert.Single(snapshot.Turns).Items.Single(
            item => item.Content.GetProperty("type").GetString() == "function_call");
        CanonicalModelItem restored = Assert.Single(snapshot.Turns[0].ModelSteps).Response.Output[0];
        Assert.Equal(CALL_JSON, original.Content.GetRawText());
        Assert.NotEqual(original.Content.GetRawText(), restored.Content.GetRawText());
        Assert.True(JsonElement.DeepEquals(original.Content, restored.Content));
        Assert.Contains(snapshot.EffectiveTurns[0].Items, item =>
            item.Content.GetProperty("type").GetString() == "function_call_output" &&
            item.Content.GetProperty("output").GetString()!.Contains("tool_outcome_unknown", StringComparison.Ordinal));
        Assert.Equal(before, OriginalHistory(restarted));
        Assert.Equal(1, fixture.HandlerCalls);
    }

    /// <summary>Recovery по-прежнему отклоняет подменённый original JSON, включая неизвестные поля и порядок массива.</summary>
    [Theory]
    [InlineData("call_id")]
    [InlineData("arguments")]
    [InlineData("unknown")]
    [InlineData("array")]
    public async Task RecoveryReadRejectsChangedOriginalContent(string change)
    {
        // Arrange
        await using CatalogRunnerFixture fixture = new();
        DialogContinuationState pending = await SaveUnknownCallAsync(fixture);
        ServiceResult<DialogRecoveryResult> recovered = await fixture.Storage.Recovery.RecoverAsync(
            RecoveryRequest(fixture.Storage, pending), TestContext.Current.CancellationToken);
        Assert.True(recovered.Success, recovered.Error?.Message);
        JsonNode changed = JsonNode.Parse(CALL_JSON)!;
        switch (change)
        {
            case "call_id":
                changed["call_id"] = "different-call";
                break;
            case "arguments":
                changed["arguments"] = "{\"different\":true}";
                break;
            case "unknown":
                changed["unknown"]!["label"] = "Подмена";
                break;
            case "array":
                changed["unknown"]!["ordered"] = new JsonArray(2, 1);
                break;
        }

        fixture.Storage.Committed.Items.Single(item => item.Sequence == 2).ContentJson = changed.ToJsonString();
        CatalogWriteFixture restarted = new(fixture.Storage.Committed);
        restarted.Reload();

        // Act
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            restarted.Reader.ReadCatalogAsync(restarted.FullAccess(recovered.Data!.Continuation.Token),
                TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("Recovery original position mismatch.", error.Message);
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Single(fixture.Requests);
    }

    /// <summary>Изменяет ровно проверяемое содержимое или позицию; форматирование само по себе не является подменой.</summary>
    private static CanonicalModelItem[] ChangedItems(CanonicalModelItem answer, CanonicalModelItem reasoning, string change)
    {
        if (change == "missing-item")
        {
            return [answer];
        }

        if (change == "reordered-items")
        {
            return [reasoning, answer];
        }

        JsonNode changed = JsonNode.Parse(ASSISTANT_JSON)!;
        switch (change)
        {
            case "text":
                changed["content"]![0]!["text"] = "Другой ответ";
                break;
            case "unknown":
                changed["unknown"]!["label"] = "Подмена";
                break;
            case "array":
                changed["unknown"]!["ordered"] = new JsonArray(2, 1);
                break;
        }

        return [Item(changed.ToJsonString()), reasoning];
    }

    /// <summary>Создаёт durable Unknown реальным runner/executor на isolated gateway и base storage doubles.</summary>
    private static async Task<DialogContinuationState> SaveUnknownCallAsync(CatalogRunnerFixture fixture)
    {
        DialogCatalogState created = await fixture.Storage.CreateAsync();
        fixture.Responses.Enqueue(ModelResponse.Completed([Item(CALL_JSON)]));
        fixture.HandlerAction = (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Fail(
            new ServiceError(ServiceErrorType.Timeout, "isolated unknown outcome")));
        AgentRunResult result = await fixture.RunAsync(created.Token, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.True(result.TerminalSaved, result.Error?.Message);
        Assert.Equal(1, fixture.HandlerCalls);
        DialogContinuationState pending = (await fixture.Storage.SnapshotAsync(result.Token!)).Continuation!;
        Assert.Equal(DialogReadiness.RecoveryRequired, pending.Readiness);
        Assert.Equal(ToolAttemptState.Unknown, Assert.Single(pending.Pending).State);

        return pending;
    }

    /// <summary>Явное подтверждение Unknown только для isolated сценария; production recovery автоматически не выполняется.</summary>
    private static DialogRecoveryRequest RecoveryRequest(CatalogWriteFixture fixture, DialogContinuationState pending)
    {
        DialogPendingCall call = Assert.Single(pending.Pending);

        return new DialogRecoveryRequest(fixture.Access(pending.Token), pending.Token, Guid.NewGuid(),
            pending.RecoveryRevision, pending.FencingEpoch, call.TurnId,
            [new DialogRecoveryResolution(call.TurnId, call.StepId, call.OutputIndex,
                DialogRecoveryKind.AcknowledgedUnknown, "isolated-explicit-ack")]);
    }

    /// <summary>Фиксирует originals отдельно от append-only recovery projection.</summary>
    private static string OriginalHistory(CatalogWriteFixture fixture)
    {
        return JsonSerializer.Serialize(new
        {
            fixture.Committed.Items, fixture.Committed.Steps, fixture.Committed.Turns, fixture.Committed.Contexts
        });
    }

    /// <summary>Сохраняет исходные пробелы, кириллицу и неизвестные поля в независимом canonical snapshot.</summary>
    private static CanonicalModelItem Item(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new CanonicalModelItem(document.RootElement);
    }
}
