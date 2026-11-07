using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiGenerationFixture;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Actual bundle передаёт полный canonical request и читает один JSON/SSE response.</summary>
public class Sub2ApiGenerationTests
{
    /// <summary>Порядок protocol indices, arguments delta и opaque item сохраняются до terminal либо EOF.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FunctionArgumentsAndOpaqueOrderingSurvive(bool terminal)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        string text = Frame("""{"type":"response.output_item.done","output_index":1,"item":{"type":"reasoning","encrypted_content":"opaque","future":true}}""")
            + Frame("""{"type":"response.output_item.added","output_index":0,"item":{"id":"f","type":"function_call","call_id":"c","name":"tool","arguments":"","future":3}}""")
            + Frame("""{"type":"response.function_call_arguments.delta","output_index":0,"item_id":"f","delta":"{\"x\":"}""")
            + Frame("""{"type":"response.function_call_arguments.delta","output_index":0,"item_id":"f","delta":"1}"}""");
        if (terminal)
        {
            text += Frame("""{"type":"response.completed","response":{"status":"completed","output":[]}}""");
        }

        fixture.Reply(text, fragment: 1);
        List<ModelStreamUpdate> updates = [];

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            (update, _) => { updates.Add(update); return ValueTask.CompletedTask; }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Data!.Output.Count);
        Assert.Equal("function_call", result.Data.Output[0].Content.GetProperty("type").GetString());
        Assert.Equal("{\"x\":1}", result.Data.Output[0].Content.GetProperty("arguments").GetString());
        Assert.Equal(3, result.Data.Output[0].Content.GetProperty("future").GetInt32());
        Assert.Equal("opaque", result.Data.Output[1].Content.GetProperty("encrypted_content").GetString());
        Assert.NotNull(Assert.Single(updates).Item);
        Assert.Null(result.Data.Continuation);
        if (terminal)
        {
            Assert.Equal(ModelResponseStatus.Completed, result.Data.Status);
        }
        else
        {
            Assert.Equal(ModelResponseStatus.Incomplete, result.Data.Status);
        }
    }

    /// <summary>Каждый send содержит compact-окно, хвост/input, reasoning, instructions/tools/effort без anchors.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullContextAndNextToolPairAreSentWithoutContinuation(bool streaming)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        List<JsonElement> sent = [];
        fixture.Handler.Respond = async (http, ct) =>
        {
            string body = await http.Content!.ReadAsStringAsync(ct);
            sent.Add(JsonSerializer.Deserialize<JsonElement>(body));
            Assert.False(http.Headers.Contains("x-codex-turn-state"));
            return Sub2ApiCatalogFixture.Json(COMPLETED);
        };
        CanonicalModelItem[] input =
        [
            new(Element("""{"type":"compaction","encrypted_content":"accepted-window","future":[1,2]}""")),
            new(Element("""{"type":"reasoning","content":[{"type":"reasoning_text","text":"original"}],"encrypted_content":"opaque-input"}""")),
            new(Element("""{"type":"message","role":"user","content":[{"type":"input_text","text":"tail"}]}""")),
            new(Element("""{"type":"message","role":"user","content":[{"type":"input_text","text":"current"}]}"""))
        ];
        JsonElement schema = Element("""{"type":"object","properties":{},"future":{"keep":true}}""");
        ModelToolDefinition tool = new("tool", "business tool", schema, true);
        ModelRequestParameters parameters = new(Element("""{"text":{"verbosity":"low","future":true},"reasoning":{"summary":"auto"},"parallel_tool_calls":false}"""));
        ModelRequest first = new("public-model", "medium", "original instructions", input, [tool], parameters: parameters);
        List<ModelStreamUpdate> updates = [];
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            callback = (update, _) => { updates.Add(update); return ValueTask.CompletedTask; };
        }

        // Act
        ServiceResult<ModelResponse> initial = await fixture.Gateway.GenerateAsync(fixture.Call, first, fixture.Access, callback,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(initial.Success);
        Assert.Equal(ModelResponseStatus.Completed, initial.Data!.Status);

        // Arrange
        CanonicalModelItem result = new(Element("""{"type":"function_call_output","call_id":"c","output":"business-result","future":19}"""));
        CanonicalModelItem[] nextInput = [..input, ..initial.Data!.Output, result];
        ModelRequest next = new(first.Model, first.ReasoningEffort, first.Instructions, nextInput, first.Tools, parameters: parameters);

        // Act
        ServiceResult<ModelResponse> second = await fixture.Gateway.GenerateAsync(fixture.Call, next, fixture.Access, callback,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(second.Success);
        Assert.Equal(ModelResponseStatus.Completed, second.Data!.Status);
        Assert.Null(initial.Data.Continuation);
        Assert.Null(second.Data.Continuation);
        Assert.Equal("requested-echo", second.Data.Envelope!.Content.GetProperty("model").GetString());
        Assert.Equal(2, fixture.Handler.Calls);
        Assert.Equal(1, fixture.FactoryCalls);
        Assert.All(fixture.Handler.Urls, url => Assert.Equal("https://gateway.invalid/prefix/v1/responses", url));
        Assert.All(fixture.Handler.Authorizations, auth => Assert.Equal("Bearer " + Sub2ApiCatalogFixture.INDIVIDUAL_KEY, auth));
        Assert.Equal(0, fixture.KeySource.Calls);
        Assert.Equal(streaming, sent[0].GetProperty("stream").GetBoolean());
        Assert.False(sent[0].GetProperty("store").GetBoolean());
        Assert.Equal("public-model", sent[0].GetProperty("model").GetString());
        Assert.Equal("medium", sent[0].GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("auto", sent[0].GetProperty("reasoning").GetProperty("summary").GetString());
        Assert.Equal(first.Instructions, sent[0].GetProperty("instructions").GetString());
        Assert.True(JsonElement.DeepEquals(schema, sent[0].GetProperty("tools")[0].GetProperty("parameters")));
        Assert.True(sent[0].GetProperty("text").GetProperty("future").GetBoolean());
        Assert.All(sent, body => Assert.False(body.TryGetProperty("previous_response_id", out _)));
        Assert.Equal(nextInput.Length, sent[1].GetProperty("input").GetArrayLength());
        for (int index = 0; index < nextInput.Length; index++)
        {
            Assert.True(JsonElement.DeepEquals(nextInput[index].Content, sent[1].GetProperty("input")[index]));
        }

        if (streaming)
        {
            Assert.Equal(4, updates.Count);
        }

        string logs = string.Join("", fixture.Logs.Entries);
        Assert.DoesNotContain("opaque-", logs);
        Assert.DoesNotContain("accepted-window", logs);
        Assert.DoesNotContain("business-result", logs);
        Assert.DoesNotContain(Sub2ApiCatalogFixture.INDIVIDUAL_KEY, logs);
    }

    /// <summary>Один send читает SSE даже для non-stream; terminal envelope сохраняется полностью.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 11)]
    public async Task SsePreservesCanonicalTerminalAndRequestedEcho(bool streaming, int fragment)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiTestStream stream = fixture.Reply(Partial() + Terminal() + Frame("not-json-after-terminal"), fragment: fragment);
        List<ModelStreamUpdate> updates = [];
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            callback = (update, _) => { updates.Add(update); return ValueTask.CompletedTask; };
        }

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access, callback,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ModelResponseStatus.Completed, result.Data!.Status);
        Assert.True(JsonElement.DeepEquals(Element(COMPLETED), result.Data.Envelope!.Content));
        Assert.Equal("opaque-result", result.Data.Output[0].Content.GetProperty("encrypted_content").GetString());
        Assert.Null(result.Data.Continuation);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        if (streaming)
        {
            Assert.Equal("hello", Assert.Single(updates).TextDelta);
        }
    }

    /// <summary>EOF/[DONE]/незакрытый terminal frame не подтверждают Completed.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("data: [DONE]\n\n")]
    [InlineData("data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[]}}")]
    public async Task PartialOutputSurvivesWithoutTerminal(string suffix)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        fixture.Reply(Partial() + suffix);

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModelResponseStatus.Incomplete, result.Data!.Status);
        Assert.Equal("hello", Assert.Single(result.Data.Output).Content.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(17, result.Data.Output[0].Content.GetProperty("future").GetInt32());
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Contains(fixture.Logs.Entries, entry => entry.Contains("Outcome=Incomplete", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Logs.Entries, entry => entry.Contains("Outcome=Completed", StringComparison.Ordinal));
    }

    /// <summary>Unary lifecycle зависит от terminal status и output, а не HTTP200/id/model.</summary>
    [Theory]
    [InlineData("{\"output\":[]}", ModelResponseStatus.Incomplete)]
    [InlineData("{\"status\":\"completed\"}", ModelResponseStatus.Incomplete)]
    [InlineData("{\"status\":\"failed\",\"output\":[]}", ModelResponseStatus.Failed)]
    [InlineData("{\"status\":\"completed\",\"output\":[],\"error\":{\"message\":\"private\"}}", ModelResponseStatus.Failed)]
    public async Task JsonLifecycleRequiresTerminalConfirmation(string json, ModelResponseStatus expected)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiTestStream stream = fixture.Reply(json, "application/json");

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Data!.Status);
        Assert.Null(result.Data.Continuation);
        Assert.True(JsonElement.DeepEquals(Element(json), result.Data.Envelope!.Content));
        Assert.DoesNotContain("private", result.Data.Error?.Message ?? "");
        Assert.True(stream.Disposed);
        Assert.Contains(fixture.Logs.Entries, entry => entry.Contains("Outcome=" + expected, StringComparison.Ordinal));
    }

    /// <summary>Неподдержанные controls отказывают до factory без потери canonical input.</summary>
    [Fact]
    public async Task UnsupportedControlDoesNotSend()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        ModelRequest request = new("public-model", "medium", "", [], [], parameters: new(Element("""{"previous_response_id":"secret"}""")));

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.Equal("secret", request.Parameters!.Content.GetProperty("previous_response_id").GetString());
    }

    /// <summary>Создаёт независимый JSON для синтетических canonical fixtures.</summary>
    private static JsonElement Element(string json)
    {
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
