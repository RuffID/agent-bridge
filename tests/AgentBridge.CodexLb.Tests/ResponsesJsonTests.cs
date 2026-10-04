using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Responses;
using AgentBridge.Domain.Dialogs;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Public DI → gateway → actual HttpClientLibrary → fake handler/streams, без hosting и сети.</summary>
public class ResponsesJsonTests
{
    private const string KEY = "synthetic-individual-key";
    private const string PRIVATE = "synthetic-private-secret";
    private const string OUTPUT = """
        [{"type":"function_call","name":"lookup","call_id":"call-1","arguments":"{\"id\":1}","future":{"x":[null,3]}},
         {"type":"reasoning","id":"r1","encrypted_content":"opaque-reasoning","summary":[]},
         {"type":"compaction","encrypted_content":"opaque-compaction","unknown":true}]
        """;

    /// <summary>Canonical input/output/tools/controls не сокращаются до текста; exact selection и route сохраняются.</summary>
    [Fact]
    public async Task CanonicalRoundTripPreservesOpaqueOrderAndIndependentSnapshots()
    {
        using Fixture fixture = new();
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.IsType<HttpApiClient>(fixture.Scope.ServiceProvider.GetRequiredService<HttpApiClient>());
        using JsonDocument source = JsonDocument.Parse("""
            {"input":[{"role":"user","content":[{"type":"input_text","text":"question"},{"type":"input_image","image_url":"opaque"}],"unknown":1},
            {"type":"function_call","call_id":"call-1","arguments":"{}"},
            {"type":"function_call_output","call_id":"call-1","output":{"data":[1,null]},"unknown":false},
            {"type":"reasoning","encrypted_content":"opaque-input","summary":[]},
            {"type":"compaction","encrypted_content":"opaque-state"}],
            "parameters":{"tool_choice":{"type":"function","name":"lookup","future":[1]},"parallel_tool_calls":false,
            "include":["reasoning.encrypted_content"],"service_tier":"priority","truncation":"disabled","prompt_cache_key":"cache",
            "text":{"format":{"type":"json_schema","schema":{"type":"object","properties":{"x":{"type":"string"}},"future":null}},"future":true},
            "reasoning":{"summary":"detailed","future":{"opaque":1}}},
            "schema":{"type":"object","properties":{"id":{"type":"integer"}},"additionalProperties":false,"required":["id"],"future":true}}
            """);
        JsonElement root = source.RootElement;
        List<CanonicalModelItem> input = root.GetProperty("input").EnumerateArray().Select(item => new CanonicalModelItem(item)).ToList();
        ModelRequest request = new("Exact-Model", "EXACT-effort", "instructions", input,
            [new("lookup", "description", root.GetProperty("schema"), true)],
            parameters: new(root.GetProperty("parameters")));
        string expectedInput = root.GetProperty("input").GetRawText();
        string expectedParameters = root.GetProperty("parameters").GetRawText();
        source.Dispose();
        input.Clear();
        fixture.SetJson("{\"id\":\"resp-1\",\"object\":\"response\",\"status\":\"completed\",\"model\":\"server-model\",\"output\":"
            + OUTPUT + ",\"usage\":{\"input_tokens\":7,\"future\":true},\"future\":{\"value\":[1,null]}}");
        fixture.Handler.Headers["X-Codex-Turn-State"] = "opaque-turn";
        ServiceResult<ModelResponse> result = await fixture.Generate(request);
        Assert.True(result.Success);
        ModelResponse report = result.Data!;
        Assert.Equal(ModelResponseStatus.Completed, report.Status);
        AssertJson(OUTPUT, JsonSerializer.Serialize(report.Output.Select(item => item.Content)));
        JsonElement sent = fixture.Handler.Bodies.Single();
        AssertJson(expectedInput, sent.GetProperty("input").GetRawText());
        Assert.Equal("Exact-Model", sent.GetProperty("model").GetString());
        Assert.Equal("EXACT-effort", sent.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("instructions", sent.GetProperty("instructions").GetString());
        Assert.False(sent.GetProperty("stream").GetBoolean());
        Assert.False(sent.GetProperty("store").GetBoolean());
        using JsonDocument controls = JsonDocument.Parse(expectedParameters);
        foreach (JsonProperty control in controls.RootElement.EnumerateObject())
        {
            if (control.Name == "reasoning")
            {
                Assert.True(sent.GetProperty("reasoning").GetProperty("future").GetProperty("opaque").GetInt32() == 1);
            }
            else { AssertJson(control.Value.GetRawText(), sent.GetProperty(control.Name).GetRawText()); }
        }
        JsonElement tool = sent.GetProperty("tools")[0];
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.True(tool.GetProperty("strict").GetBoolean());
        Assert.True(tool.GetProperty("parameters").GetProperty("future").GetBoolean());
        Assert.Equal("https://gateway.invalid/prefix/v1/responses", fixture.Handler.Urls.Single());
        Assert.Equal("POST", fixture.Handler.Methods.Single());
        Assert.Equal("Bearer " + KEY, fixture.Handler.Authorizations.Single());
        Assert.Null(fixture.Http.DefaultRequestHeaders.Authorization);
        Assert.Equal(7, report.Envelope!.Content.GetProperty("usage").GetProperty("input_tokens").GetInt32());
        Assert.Equal("server-model", report.Envelope.Content.GetProperty("model").GetString());
        Assert.Equal("opaque-turn", report.Continuation!.Content.GetProperty("headers").GetProperty("x-codex-turn-state").GetString());
        Assert.DoesNotContain(KEY, report.Continuation.Content.GetRawText());
        Assert.True(fixture.Handler.Content!.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Handler.RequestContent!.ReadAsStringAsync());
    }

    /// <summary>Lifecycle определяется полным status/error, а не 2xx либо непустым output.</summary>
    [Theory]
    [InlineData("completed", ModelResponseStatus.Completed)]
    [InlineData("incomplete", ModelResponseStatus.Incomplete)]
    [InlineData("failed", ModelResponseStatus.Failed)]
    [InlineData("in_progress", ModelResponseStatus.Incomplete)]
    [InlineData("queued", ModelResponseStatus.Incomplete)]
    [InlineData("future", ModelResponseStatus.Incomplete)]
    [InlineData("cancelled", ModelResponseStatus.Incomplete)]
    [InlineData(null, ModelResponseStatus.Incomplete)]
    public async Task LifecycleKeepsOutputAndFullEnvelope(string? status, ModelResponseStatus expected)
    {
        using Fixture fixture = new();
        fixture.SetJson("{\"status\":" + JsonSerializer.Serialize(status) + ",\"id\":\"resp-life\",\"output\":" + OUTPUT
            + ",\"incomplete_details\":{\"reason\":\"max_output_tokens\"},\"future\":{\"private\":\"" + PRIVATE + "\"}}");
        ServiceResult<ModelResponse> result = await fixture.Generate(Request());
        Assert.True(result.Success);
        Assert.Equal(expected, result.Data!.Status);
        AssertJson(OUTPUT, JsonSerializer.Serialize(result.Data.Output.Select(item => item.Content)));
        Assert.Equal(PRIVATE, result.Data.Envelope!.Content.GetProperty("future").GetProperty("private").GetString());
        AssertSafe(fixture.Log.Text);
        Assert.True(fixture.Handler.Content!.Disposed);
    }

    /// <summary>Explicit error даже при completed сохраняется в Failed отчёте без публикации raw message.</summary>
    [Fact]
    public async Task ExplicitModelErrorKeepsProtocolButProjectsSafeError()
    {
        using Fixture fixture = new();
        fixture.SetJson("{\"status\":\"completed\",\"output\":" + OUTPUT
            + ",\"error\":{\"type\":\"server_error\",\"code\":\"server_error\",\"message\":\"" + PRIVATE + "\",\"unknown\":123}}");
        ModelResponse report = (await fixture.Generate(Request())).Data!;
        Assert.Equal(ModelResponseStatus.Failed, report.Status);
        CodexLbServiceError error = Assert.IsType<CodexLbServiceError>(report.Error);
        Assert.Null(error.HttpStatus);
        Assert.Equal("server_error", error.Code);
        Assert.Equal(PRIVATE, report.Envelope!.Content.GetProperty("error").GetProperty("message").GetString());
        AssertSafe(JsonSerializer.Serialize(error) + fixture.Log.Text);
    }

    /// <summary>Completed допускает пустой output; обязательный encrypted include добавляется только при отсутствии include.</summary>
    [Fact]
    public async Task EmptyCompletedAndDefaultIncludeAreSupported()
    {
        using Fixture fixture = new();
        ModelResponse report = (await fixture.Generate(Request())).Data!;
        Assert.Equal(ModelResponseStatus.Completed, report.Status);
        Assert.Empty(report.Output);
        Assert.Equal("reasoning.encrypted_content", fixture.Handler.Bodies.Single().GetProperty("include")[0].GetString());
        Assert.False(fixture.Handler.Bodies.Single().TryGetProperty("reasoning", out _));
    }

    /// <summary>Даже status completed без canonical output не подтверждает полный результат; envelope остаётся доступным.</summary>
    [Fact]
    public async Task CompletedWithoutOutputIsIncomplete()
    {
        using Fixture fixture = new();
        fixture.SetJson("{\"status\":\"completed\",\"id\":\"resp-missing-output\",\"future\":true}");
        ModelResponse report = (await fixture.Generate(Request())).Data!;
        Assert.Equal(ModelResponseStatus.Incomplete, report.Status);
        Assert.Empty(report.Output);
        Assert.True(report.Envelope!.Content.GetProperty("future").GetBoolean());
    }

    /// <summary>Полный HTTP typed failure не подменяется caller cancellation на освобождении ответа.</summary>
    [Fact]
    public async Task CompleteHttpFailureKeepsPriorityOverLateCancellation()
    {
        using Fixture fixture = new();
        using CancellationTokenSource caller = new();
        CancelOnDisposeStream stream = new(Encoding.UTF8.GetBytes("{\"error\":{\"type\":\"invalid_request_error\",\"code\":\"invalid_value\",\"param\":\"input\"}}"), caller);
        fixture.Handler.Respond = (_, _) =>
        {
            StreamContent content = new(stream);
            content.Headers.ContentType = new("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content });
        };
        ServiceResult<ModelResponse> result = await fixture.Generate(Request(), ct: caller.Token);
        Assert.True(caller.IsCancellationRequested);
        CodexLbServiceError error = Assert.IsType<CodexLbServiceError>(result.Error);
        Assert.Equal(400, error.HttpStatus);
        Assert.Equal("invalid_value", error.Code);
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Статусный отказ стабилен, complete error fields фильтруются, retry/общего ключа нет.</summary>
    [Theory]
    [InlineData(400, ServiceErrorType.Validation)]
    [InlineData(401, ServiceErrorType.Unauthorized)]
    [InlineData(403, ServiceErrorType.Forbidden)]
    [InlineData(404, ServiceErrorType.NotFound)]
    [InlineData(408, ServiceErrorType.Timeout)]
    [InlineData(409, ServiceErrorType.Conflict)]
    [InlineData(422, ServiceErrorType.Validation)]
    [InlineData(429, ServiceErrorType.Rejected)]
    [InlineData(500, ServiceErrorType.Rejected)]
    [InlineData(504, ServiceErrorType.Timeout)]
    public async Task HttpErrorsAreStableSafeAndNeverRetried(int status, ServiceErrorType type)
    {
        using Fixture fixture = new();
        fixture.Handler.Status = (HttpStatusCode)status;
        fixture.SetJson("{\"error\":{\"type\":\"invalid_request_error\",\"code\":\"previous_response_not_found\",\"param\":\"previous_response_id\",\"message\":\"" + PRIVATE + "\"}}");
        ServiceResult<ModelResponse> result = await fixture.Generate(Request());
        Assert.False(result.Success);
        Assert.Null(result.Data);
        CodexLbServiceError error = Assert.IsType<CodexLbServiceError>(result.Error);
        Assert.Equal(type, error.Type);
        Assert.Equal(status, error.HttpStatus);
        Assert.Equal("invalid_request_error", error.ApiType);
        Assert.Equal("previous_response_not_found", error.Code);
        Assert.Equal("previous_response_id", error.Param);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(["Bearer " + KEY], fixture.Handler.Authorizations);
        Assert.True(fixture.Handler.Content!.Disposed);
        AssertSafe(JsonSerializer.Serialize(error) + fixture.Log.Text);
        Assert.All(fixture.Log.Exceptions, Assert.Null);
    }

    /// <summary>Malicious/неполные/невалидные error values не извлекаются из preview либо token-подобного текста.</summary>
    [Theory]
    [InlineData("malicious")]
    [InlineData("malformed")]
    [InlineData("truncated")]
    [InlineData("unsupported")]
    [InlineData("encoding")]
    [InlineData("empty")]
    [InlineData("wrong-shape")]
    public async Task UnsafeOrIncompleteErrorHasOnlySafeStatus(string variant)
    {
        using Fixture fixture = new();
        fixture.Handler.Status = HttpStatusCode.BadRequest;
        string body = "{\"error\":{\"type\":\"" + PRIVATE + "\",\"code\":\"" + KEY + "\",\"param\":\"" + PRIVATE + "\",\"message\":\"" + KEY + "\"}}";
        if (variant == "malformed") { body = "{\"error\":"; }
        if (variant == "truncated")
        {
            body = "{\"error\":{\"type\":\"server_error\",\"code\":\"server_error\",\"param\":\"input\"},\"padding\":\"" + new string('x', 70_000) + "\"}";
        }
        if (variant == "empty") { body = ""; }
        if (variant == "wrong-shape") { body = "{\"error\":\"server_error\"}"; }
        fixture.SetJson(body);
        if (variant == "unsupported") { fixture.Handler.MediaType = "application/octet-stream"; }
        if (variant == "encoding") { fixture.Handler.Bytes = [0xFF, 0xFE, 0xFF]; }
        ServiceResult<ModelResponse> result = await fixture.Generate(Request());
        CodexLbServiceError error = Assert.IsType<CodexLbServiceError>(result.Error);
        Assert.Equal(400, error.HttpStatus);
        Assert.Null(error.ApiType);
        Assert.Null(error.Code);
        Assert.Null(error.Param);
        AssertSafe(JsonSerializer.Serialize(error) + fixture.Log.Text);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.True(fixture.Handler.Content!.Disposed);
    }

    /// <summary>Плохой JSON/shape и пустой HTTP не становятся Completed и освобождают response.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{\"status\":\"completed\",\"output\":null}")]
    [InlineData("{\"status\":\"completed\",\"output\":[1]}")]
    public async Task MalformedSuccessFailsSafelyAndDisposes(string json)
    {
        using Fixture fixture = new();
        fixture.SetJson(json);
        ServiceResult<ModelResponse> result = await fixture.Generate(Request());
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.True(fixture.Handler.Content!.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture.Log.Text + JsonSerializer.Serialize(result.Error));
    }

    /// <summary>Continuation переносит только anchor и разрешённый header; unknown metadata сохраняются вне input.</summary>
    [Fact]
    public async Task ContinuationRoundTripPreservesUnknownAndUsesOnlyAllowedHeader()
    {
        using Fixture fixture = new();
        fixture.Handler.Headers["X-Codex-Turn-State"] = "opaque-first";
        fixture.SetJson("{\"status\":\"completed\",\"id\":\"resp-first\",\"output\":[]}");
        ModelContinuation continuation = (await fixture.Generate(Request())).Data!.Continuation!;
        JsonObject content = JsonNode.Parse(continuation.Content.GetRawText())!.AsObject();
        content["future"] = JsonNode.Parse("{\"opaque\":[1,null]}");
        content["headers"]!["x-private-never-send"] = PRIVATE;
        continuation = new(JsonSerializer.SerializeToElement(content));
        fixture.SetJson("{\"status\":\"incomplete\",\"id\":\"resp-second\",\"output\":" + OUTPUT + "}");
        fixture.Handler.Headers["X-Codex-Turn-State"] = "opaque-second";
        ApplicationCallContext next = new(fixture.Call.DialogId, fixture.Call.OwnerId, Guid.NewGuid(), fixture.Call.AgentId);
        ServiceResult<ModelResponse> result = await fixture.Generate(Request(continuation), call: next);
        Assert.Equal("resp-first", fixture.Handler.Bodies[1].GetProperty("previous_response_id").GetString());
        Assert.Equal("opaque-first", fixture.Handler.TurnStates[1]);
        Assert.False(fixture.Handler.SentPrivateHeader);
        Assert.Equal("resp-second", result.Data!.Continuation!.Content.GetProperty("previous_response_id").GetString());
        Assert.Equal("opaque-second", result.Data.Continuation.Content.GetProperty("headers").GetProperty("x-codex-turn-state").GetString());
        AssertJson("{\"opaque\":[1,null]}", result.Data.Continuation.Content.GetProperty("future").GetRawText());
        Assert.Empty(fixture.Handler.Bodies[1].GetProperty("input").EnumerateArray());
        Assert.Equal("resp-first", continuation.Content.GetProperty("previous_response_id").GetString());
    }

    /// <summary>Смена key/dialog/owner/agent/endpoint не разрешает continuation и не вызывает HTTP.</summary>
    [Theory]
    [InlineData("key")]
    [InlineData("dialog")]
    [InlineData("owner")]
    [InlineData("agent")]
    [InlineData("endpoint")]
    public async Task ContinuationCannotCrossBinding(string part)
    {
        using Fixture fixture = new();
        ModelContinuation continuation = (await fixture.Generate(Request())).Data!.Continuation!;
        ApplicationCallContext call = fixture.Call;
        ModelAccess access = new(part == "key" ? "other-synthetic-key" : KEY);
        call = new(part == "dialog" ? DialogId.From(Guid.NewGuid()) : call.DialogId,
            part == "owner" ? DialogOwnerId.From("other") : call.OwnerId, Guid.NewGuid(), part == "agent" ? "other" : call.AgentId);
        using Fixture other = new(part == "endpoint" ? "https://other.invalid" : "https://gateway.invalid/prefix/");
        ServiceResult<ModelResponse> result = await other.Generate(Request(continuation), access, call);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, other.Handler.Calls);
        AssertSafe(JsonSerializer.Serialize(result.Error));
    }

    /// <summary>Unknown format и malformed bound header отказывают до HTTP.</summary>
    [Theory]
    [InlineData("unknown", ServiceErrorType.Unsupported)]
    [InlineData("header", ServiceErrorType.Validation)]
    [InlineData("anchor", ServiceErrorType.Validation)]
    public async Task ContinuationRejectsUnknownOrUnsafeShape(string variant, ServiceErrorType expected)
    {
        using Fixture fixture = new();
        ModelContinuation initial = (await fixture.Generate(Request())).Data!.Continuation!;
        JsonObject content = JsonNode.Parse(initial.Content.GetRawText())!.AsObject();
        if (variant == "unknown") { content["adapter"] = "other"; }
        if (variant == "header") { content["headers"]!["x-codex-turn-state"] = "bad\r\ninjected"; }
        if (variant == "anchor") { content["previous_response_id"] = 12; }
        ServiceResult<ModelResponse> result = await fixture.Generate(Request(new(JsonSerializer.SerializeToElement(content))));
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Параметры не могут override обязательные поля либо silently игнорироваться.</summary>
    [Theory]
    [InlineData("{\"model\":\"other\"}", ServiceErrorType.Unsupported)]
    [InlineData("{\"stream\":true}", ServiceErrorType.Unsupported)]
    [InlineData("{\"store\":true}", ServiceErrorType.Unsupported)]
    [InlineData("{\"previous_response_id\":\"other\"}", ServiceErrorType.Unsupported)]
    [InlineData("{\"future\":true}", ServiceErrorType.Unsupported)]
    [InlineData("{\"parallel_tool_calls\":\"true\"}", ServiceErrorType.Validation)]
    [InlineData("{\"reasoning\":{\"effort\":\"other\"}}", ServiceErrorType.Validation)]
    [InlineData("{\"include\":[1]}", ServiceErrorType.Validation)]
    [InlineData("{\"text\":null}", ServiceErrorType.Validation)]
    [InlineData("{\"truncation\":\"bad\"}", ServiceErrorType.Validation)]
    [InlineData("{\"include\":[],\"include\":[]}", ServiceErrorType.Validation)]
    public async Task InvalidParametersFailBeforeHttp(string json, ServiceErrorType expected)
    {
        using Fixture fixture = new();
        using JsonDocument document = JsonDocument.Parse(json);
        ModelRequest request = new("model", null, "", [], [], parameters: new(document.RootElement));
        ServiceResult<ModelResponse> result = await fixture.Generate(request);
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Компакт ещё не реализован, без HTTP side effects.</summary>
    [Fact]
    public async Task CompactIsExplicitlyUnsupported()
    {
        using Fixture fixture = new();
        IModelGateway gateway = fixture.Gateway;
        ServiceResult<ModelResponse> compact = await gateway.CompactAsync(fixture.Call, Request(), new(KEY));
        Assert.Equal(ServiceErrorType.Unsupported, compact.Error!.Type);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Caller cancellation до HTTP сохраняет исходный токен.</summary>
    [Fact]
    public async Task PreCanceledCallerDoesNotSend()
    {
        using Fixture fixture = new();
        using CancellationTokenSource caller = new();
        caller.Cancel();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Generate(Request(), ct: caller.Token));
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Caller cancellation во время отправки не превращается в deadline и не повторяет запрос.</summary>
    [Fact]
    public async Task CallerCancellationDuringSendUsesOriginalToken()
    {
        using Fixture fixture = new();
        using CancellationTokenSource caller = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException();
        };
        Task<ServiceResult<ModelResponse>> pending = fixture.Generate(Request(), ct: caller.Token);
        await started.Task;
        caller.Cancel();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.Equal(1, fixture.Handler.Calls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Handler.RequestContent!.ReadAsStringAsync());
    }

    /// <summary>Конечный deadline ограничивает отправку; caller имеет приоритет при одновременной отмене.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineAndSimultaneousCallerAreDistinct(bool cancelCaller)
    {
        using Fixture fixture = new(timeout: TimeSpan.FromMilliseconds(60));
        using CancellationTokenSource caller = new();
        fixture.Handler.Respond = async (_, ct) =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException)
            {
                if (cancelCaller) { caller.Cancel(); }
                throw;
            }
            throw new InvalidOperationException();
        };
        if (cancelCaller)
        {
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Generate(Request(), ct: caller.Token));
            Assert.Equal(caller.Token, error.CancellationToken);
        }
        else { Assert.Equal(ServiceErrorType.Timeout, (await fixture.Generate(Request(), ct: caller.Token)).Error!.Type); }
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Deadline/caller действуют на чтение JSON после headers; локальный поток освобождается.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringBodyReadDisposesOwnedResponse(bool cancelCaller)
    {
        using Fixture fixture = new(timeout: cancelCaller ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(60));
        using CancellationTokenSource caller = new();
        BlockingStream stream = new();
        fixture.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        Task<ServiceResult<ModelResponse>> pending = fixture.Generate(Request(), ct: caller.Token);
        await stream.Started.Task;
        if (cancelCaller)
        {
            caller.Cancel();
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(caller.Token, error.CancellationToken);
        }
        else { Assert.Equal(ServiceErrorType.Timeout, (await pending).Error!.Type); }
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Поздняя caller cancellation после получения полного JSON сохраняет отчёт; explicit failure приоритетнее.</summary>
    [Theory]
    [InlineData("completed", ModelResponseStatus.Canceled)]
    [InlineData("incomplete", ModelResponseStatus.Canceled)]
    [InlineData("failed", ModelResponseStatus.Failed)]
    public async Task LateCancellationKeepsCanonicalReportAndTypedFailure(string status, ModelResponseStatus expected)
    {
        using Fixture fixture = new();
        using CancellationTokenSource caller = new();
        string json = "{\"id\":\"resp-late\",\"status\":\"" + status + "\",\"output\":" + OUTPUT
            + ",\"usage\":{\"output_tokens\":17},\"future\":true}";
        CancelOnDisposeStream stream = new(Encoding.UTF8.GetBytes(json), caller);
        fixture.Handler.Respond = (_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Headers.Add("x-codex-turn-state", "late-turn");
            return Task.FromResult(response);
        };
        ServiceResult<ModelResponse> result = await fixture.Generate(Request(), ct: caller.Token);
        Assert.True(caller.IsCancellationRequested);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Data!.Status);
        AssertJson(OUTPUT, JsonSerializer.Serialize(result.Data.Output.Select(item => item.Content)));
        Assert.Equal(17, result.Data.Envelope!.Content.GetProperty("usage").GetProperty("output_tokens").GetInt32());
        Assert.Equal("resp-late", result.Data.Continuation!.Content.GetProperty("previous_response_id").GetString());
        Assert.Equal("late-turn", result.Data.Continuation.Content.GetProperty("headers").GetProperty("x-codex-turn-state").GetString());
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Новый envelope без пригодного id не сохраняет устаревший anchor, полный исходный id остаётся в envelope.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("\"bad id\"")]
    [InlineData("\"\"")]
    public async Task MissingOrInvalidNewIdNeverReusesOldAnchor(string? idJson)
    {
        using Fixture fixture = new();
        ModelContinuation old = (await fixture.Generate(Request())).Data!.Continuation!;
        fixture.SetJson("{\"status\":\"completed\",\"output\":[],\"future\":true"
            + (idJson is null ? "" : ",\"id\":" + idJson) + "}");
        ModelResponse report = (await fixture.Generate(Request(old))).Data!;
        Assert.False(report.Continuation!.Content.TryGetProperty("previous_response_id", out _));
        Assert.Equal("resp-default", old.Content.GetProperty("previous_response_id").GetString());
        Assert.True(report.Envelope!.Content.GetProperty("future").GetBoolean());
        if (idJson is not null) { AssertJson(idJson, report.Envelope.Content.GetProperty("id").GetRawText()); }
        await fixture.Generate(Request(report.Continuation));
        Assert.False(fixture.Handler.Bodies[2].TryGetProperty("previous_response_id", out _));
    }

    /// <summary>Отмена чтения HTTP-error body освобождает response и сохраняет caller/deadline источник.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringErrorBodyDisposes(bool cancelCaller)
    {
        using Fixture fixture = new(timeout: cancelCaller ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(60));
        using CancellationTokenSource caller = new();
        BlockingStream stream = new();
        fixture.Handler.Respond = (_, _) =>
        {
            StreamContent content = new(stream);
            content.Headers.ContentType = new("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content });
        };
        Task<ServiceResult<ModelResponse>> pending = fixture.Generate(Request(), ct: caller.Token);
        await stream.Started.Task;
        if (cancelCaller)
        {
            caller.Cancel();
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(caller.Token, error.CancellationToken);
        }
        else { Assert.Equal(ServiceErrorType.Timeout, (await pending).Error!.Type); }
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Ошибка чтения после headers не становится synthetic HTTP rejection и освобождает stream.</summary>
    [Fact]
    public async Task BodyIoFailurePropagatesAndDisposes()
    {
        using Fixture fixture = new();
        IOException failure = new(PRIVATE);
        FailingStream stream = new(failure);
        fixture.Handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        Exception actual = await Assert.ThrowsAsync<IOException>(() => fixture.Generate(Request()));
        Assert.Same(failure, actual);
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture.Log.Text);
        Assert.All(fixture.Log.Exceptions, Assert.Null);
    }

    /// <summary>Disconnect/неожиданный I/O распространяется без retry и raw exception в logger.</summary>
    [Fact]
    public async Task UnexpectedIoPropagatesWithoutRetryOrSecretLogging()
    {
        using Fixture fixture = new();
        HttpRequestException failure = new(PRIVATE);
        fixture.Handler.Respond = (_, _) => Task.FromException<HttpResponseMessage>(failure);
        Exception actual = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Generate(Request()));
        Assert.Same(failure, actual);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture.Log.Text);
        Assert.All(fixture.Log.Exceptions, Assert.Null);
    }

    /// <summary>Создаёт минимальный canonical request.</summary>
    private static ModelRequest Request(ModelContinuation? continuation = null) => new("model", null, "", [], [], continuation);
    /// <summary>Проверяет JSON без зависимости от whitespace.</summary>
    private static void AssertJson(string expected, string actual) => Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)));
    /// <summary>Проверяет отсутствие синтетических секретов в публичных errors и raw/форматированном log state.</summary>
    private static void AssertSafe(string text)
    {
        Assert.DoesNotContain(KEY, text);
        Assert.DoesNotContain(PRIVATE, text);
        Assert.DoesNotContain("https://gateway", text);
    }

    /// <summary>Изолированный app-owned DI root/scope/HttpClient без host.</summary>
    private class Fixture : IDisposable
    {
        internal readonly Handler Handler = new();
        internal readonly LogProvider Log = new();
        internal readonly HttpClient Http;
        internal readonly ServiceProvider Root;
        internal readonly IServiceScope Scope;
        internal ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        internal IModelGateway Gateway => Scope.ServiceProvider.GetRequiredService<IModelGateway>();

        /// <summary>Регистрирует public adapter pipeline с безопасным collecting logger.</summary>
        internal Fixture(string endpoint = "https://gateway.invalid/prefix/", TimeSpan? timeout = null)
        {
            Http = new(Handler) { Timeout = Timeout.InfiniteTimeSpan };
            ServiceCollection services = new();
            services.AddLogging(builder => builder.AddProvider(Log));
            services.AddCodexLbConfiguration(options =>
            {
                options.BaseAddress = endpoint;
                options.Model = "model";
                options.SharedApiKey = "synthetic-shared-key";
                options.GenerationTimeout = timeout ?? TimeSpan.FromSeconds(5);
            });
            services.AddScoped<IIndividualModelKeySource, KeySource>();
            services.AddCodexLbResponses(_ => Http, new() { ErrorContentMode = HttpErrorContentLogMode.JsonStructure });
            Root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Scope = Root.CreateScope();
        }

        /// <summary>Задаёт синтетический HTTP JSON.</summary>
        internal void SetJson(string body) => Handler.Bytes = Encoding.UTF8.GetBytes(body);
        /// <summary>Выбирает доступ публичным resolver и вызывает публичный gateway.</summary>
        internal async Task<ServiceResult<ModelResponse>> Generate(ModelRequest request, ModelAccess? access = null,
            ApplicationCallContext? call = null, CancellationToken ct = default)
        {
            ApplicationCallContext current = call ?? Call;
            if (access is null)
            {
                ServiceResult<ModelAccess> resolved = await Scope.ServiceProvider.GetRequiredService<IModelAccessResolver>().ResolveAsync(current.OwnerId, ct);
                access = resolved.Data!;
            }
            return await Gateway.GenerateAsync(current, request, access, cancellationToken: ct);
        }

        /// <inheritdoc/>
        public void Dispose() { Scope.Dispose(); Root.Dispose(); Http.Dispose(); }
    }

    /// <inheritdoc/>
    private class KeySource : IIndividualModelKeySource
    {
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default) => Task.FromResult<string?>(KEY);
    }

    /// <summary>Синтетический HTTP transport; фиксирует public wire data без реальных запросов.</summary>
    private class Handler : HttpMessageHandler
    {
        internal int Calls;
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal byte[] Bytes = Encoding.UTF8.GetBytes("{\"id\":\"resp-default\",\"status\":\"completed\",\"output\":[]}");
        internal string MediaType = "application/json";
        internal readonly Dictionary<string, string> Headers = new();
        internal readonly List<JsonElement> Bodies = [];
        internal readonly List<string> Urls = [];
        internal readonly List<string> Methods = [];
        internal readonly List<string?> Authorizations = [];
        internal readonly List<string?> TurnStates = [];
        internal bool SentPrivateHeader;
        internal TrackingContent? Content;
        internal HttpContent? RequestContent;
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond;
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Urls.Add(request.RequestUri!.AbsoluteUri);
            Methods.Add(request.Method.Method);
            Authorizations.Add(request.Headers.Authorization?.ToString());
            TurnStates.Add(request.Headers.TryGetValues("x-codex-turn-state", out IEnumerable<string>? values) ? values.Single() : null);
            SentPrivateHeader |= request.Headers.Contains("x-private-never-send");
            RequestContent = request.Content;
            using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Bodies.Add(document.RootElement.Clone());
            if (Respond is not null) { return await Respond(request, ct); }
            Content = new(Bytes);
            Content.Headers.ContentType = new(MediaType);
            Content.Headers.Add("X-Private-Content", PRIVATE);
            HttpResponseMessage response = new(Status) { Content = Content, ReasonPhrase = PRIVATE };
            response.Headers.Add("X-Private-Response", PRIVATE);
            foreach (KeyValuePair<string, string> header in Headers) { response.Headers.Add(header.Key, header.Value); }
            return response;
        }
    }

    /// <summary>Подтверждает освобождение response content.</summary>
    private class TrackingContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        internal bool Disposed;
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    /// <summary>Детерминированно отменяет caller после получения полного JSON при освобождении локального stream.</summary>
    private class CancelOnDisposeStream(byte[] bytes, CancellationTokenSource caller) : MemoryStream(bytes)
    {
        internal bool Disposed;
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            caller.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Локальная ошибка чтения после headers; непустая длина исключает обход чтения по Content-Length.</summary>
    private class FailingStream(IOException failure) : MemoryStream([1])
    {
        internal bool Disposed;
        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(failure);
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    /// <summary>Ожидает только cancellation при чтении, без процессов либо сети.</summary>
    private class BlockingStream : Stream
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Disposed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    /// <inheritdoc/>
    private class LogProvider : ILoggerProvider
    {
        internal string Text = "";
        internal readonly List<Exception?> Exceptions = [];
        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => new Logger(this);
        /// <inheritdoc/>
        public void Dispose() { }

        /// <inheritdoc/>
        private class Logger(LogProvider provider) : ILogger
        {
            /// <inheritdoc/>
            public bool IsEnabled(LogLevel logLevel) => true;
            /// <inheritdoc/>
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            /// <inheritdoc/>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Exceptions.Add(exception);
                provider.Text += formatter(state, exception) + JsonSerializer.Serialize(state);
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    foreach (KeyValuePair<string, object?> value in values) { provider.Text += value.Key + value.Value; }
                }
            }
        }
    }
}
