using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using Xunit;

namespace AgentBridge.Sub2Api.Tests.CompactSafety;

/// <summary>Не позволяет запустить compact либо отправить неподтверждённое окно через generation.</summary>
public class Sub2ApiCompactBoundaryTests
{
    /// <summary>Callback и родное provenance не разрешают native/legacy trigger либо opaque замену prefix.</summary>
    [Theory]
    [InlineData("compaction_trigger", false, 0)]
    [InlineData("compaction_trigger", true, 0)]
    [InlineData("compaction_trigger", false, 1)]
    [InlineData("compaction_trigger", true, 1)]
    [InlineData("compaction_trigger", false, 2)]
    [InlineData("compaction_trigger", true, 2)]
    [InlineData("compaction", false, 0)]
    [InlineData("compaction", true, 1)]
    [InlineData("compaction_summary", false, 1)]
    [InlineData("compaction_summary", true, 2)]
    [InlineData(" compaction ", true, 0)]
    public async Task GenerationRejectsCompactBeforeDispatch(string type, bool streaming, int position)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        ModelConnectorProvenance own = ((IModelContextGateway)fixture.Gateway).GetContextProvenance(fixture.Call, fixture.Access);
        List<CanonicalModelItem> input =
        [
            Item("""{"type":"message","role":"user","content":"covered-private-prefix"}"""),
            Item("""{"type":"message","role":"user","content":"uncovered-private-tail"}""")
        ];
        input.Insert(position, new(JsonSerializer.SerializeToElement(new { type, encrypted_content = "private-window", future = 19 })));
        ModelRequest request = new ModelRequest("public-model", "medium", "instructions", input, []).WithConnectorProvenance(own);
        string before = JsonSerializer.Serialize(request.Input.Select(item => item.Content));
        int updates = 0;
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            callback = (_, _) =>
            {
                updates++;
                return ValueTask.CompletedTask;
            };
        }

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, request, fixture.Access,
            callback, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.Equal(0, updates);
        Assert.Equal(before, JsonSerializer.Serialize(request.Input.Select(item => item.Content)));
        string diagnostics = result.Error.Message + string.Join("", fixture.Logs.Entries);
        Assert.DoesNotContain("private", diagnostics);
        Assert.DoesNotContain(own.Binding, diagnostics);
        Assert.Contains("ContextRejected", string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Различия first/last-property парсеров не должны обходить запрет.</summary>
    [Theory]
    [InlineData("""{"type":"compaction_trigger","type":"message","content":"private"}""")]
    [InlineData("""{"type":"message","type":"compaction_trigger","content":"private"}""")]
    [InlineData("""{"type":"compaction","type":"message","encrypted_content":"private"}""")]
    [InlineData("""{"type":"compaction_summary","type":null,"encrypted_content":"private"}""")]
    public async Task DuplicateTypeCannotHideCompact(string json)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        ModelRequest request = new ModelRequest("public-model", "medium", "instructions", [Item(json)], [])
            .WithConnectorProvenance(((IModelContextGateway)fixture.Gateway).GetContextProvenance(fixture.Call, fixture.Access));

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.Equal(json, request.Input[0].Content.GetRawText());
    }

    /// <summary>Слова о compact в пользовательском тексте не являются protocol type.</summary>
    [Fact]
    public async Task OrdinaryTextIsStillGeneration()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        CanonicalModelItem text = Item("""{"type":"message","role":"user","content":"compaction_trigger compaction compaction_summary"}""");
        ModelRequest request = new("public-model", "medium", "instructions", [text], []);
        fixture.Reply("""{"status":"completed","output":[]}""", "application/json");

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ModelResponseStatus.Completed, result.Data!.Status);
        Assert.EndsWith("/v1/responses", Assert.Single(fixture.Handler.Urls));
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Отмена compact сохраняет исходный token и не создаёт HTTP-клиент.</summary>
    [Fact]
    public async Task CanceledCompactNeverDispatches()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        using CancellationTokenSource caller = new();
        caller.Cancel();

        // Act
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Gateway.CompactAsync(fixture.Call, fixture.Request, fixture.Access, caller.Token));

        // Assert
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Фиксирует независимый canonical объект.</summary>
    private static CanonicalModelItem Item(string json)
    {
        return new(JsonSerializer.Deserialize<JsonElement>(json));
    }
}
