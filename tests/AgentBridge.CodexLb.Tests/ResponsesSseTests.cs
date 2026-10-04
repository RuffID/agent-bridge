using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Responses;
using AgentBridge.Domain.Dialogs;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Публичный DI/gateway и actual HttpClientLibrary с fragmented local SSE, без сети/hosting.</summary>
public class ResponsesSseTests
{
    private const string KEY = "synthetic-sse-key";
    private const string PRIVATE = "synthetic-private-sse";
    private const string TOOL = """{"id":"f1","type":"function_call","call_id":"c1","name":"lookup","arguments":"","future":{"opaque":[1,null]}}""";
    private const string ADDED = """{"type":"response.output_item.added","output_index":0,"item":TOOL}""";

    /// <summary>Done snapshots заменяют partial delta и сохраняют full/opaque поля итогового tool.</summary>
    [Fact]
    public async Task DoneItemsAndReasoningSummaryPreserveCanonicalState()
    {
        string done = TOOL.Replace("\"arguments\":\"\"", "\"arguments\":\"{\\\"id\\\":1}\"", StringComparison.Ordinal);
        string body = Partial()
            + Event("{\"type\":\"response.function_call_arguments.done\",\"output_index\":0,\"arguments\":\"{\\\"id\\\":1}\"}")
            + Event("{\"type\":\"response.output_item.done\",\"output_index\":0,\"item\":" + done + "}")
            + Event("{\"type\":\"response.output_text.done\",\"output_index\":1,\"content_index\":0,\"text\":\"Итог\"}")
            + Event("{\"type\":\"response.output_item.added\",\"output_index\":2,\"item\":{\"id\":\"r1\",\"type\":\"reasoning\",\"encrypted_content\":\"opaque\",\"summary\":[]}}")
            + Event("{\"type\":\"response.reasoning_summary_part.added\",\"output_index\":2,\"summary_index\":0,\"part\":{\"type\":\"summary_text\",\"text\":\"\",\"future\":1}}")
            + Event("{\"type\":\"response.reasoning_summary_text.delta\",\"output_index\":2,\"summary_index\":0,\"delta\":\"reason\"}");
        using Fixture fixture = new(body, 1);
        ModelResponse report = (await fixture.Generate()).Data!;
        Assert.Equal(ModelResponseStatus.Incomplete, report.Status);
        Assert.Equal("{\"id\":1}", report.Output[0].Content.GetProperty("arguments").GetString());
        Assert.Equal("Итог", report.Output[1].Content.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("opaque", report.Output[2].Content.GetProperty("encrypted_content").GetString());
        Assert.Equal("reason", report.Output[2].Content.GetProperty("summary")[0].GetProperty("text").GetString());
        Assert.Equal(1, report.Output[2].Content.GetProperty("summary")[0].GetProperty("future").GetInt32());
        Assert.Equal("Привет", fixture.Updates[0].TextDelta);
        Assert.Equal("{\"id\":1}", fixture.Updates[1].Item!.Content.GetProperty("arguments").GetString());
        await AssertDisposed(fixture);
    }

    /// <summary>До send отмена не вызывает HTTP/callback; во время send сохраняет исходный caller token.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerCancellationBeforeData(bool beforeSend)
    {
        using Fixture fixture = new(Terminal());
        using CancellationTokenSource caller = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async ct =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException();
        };
        if (beforeSend) { caller.Cancel(); }
        Task<ServiceResult<ModelResponse>> task = fixture.Generate(ct: caller.Token);
        try
        {
            if (!beforeSend) { await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); caller.Cancel(); }
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.Equal(caller.Token, error.CancellationToken);
            Assert.Equal(beforeSend ? 0 : 1, fixture.Handler.Calls);
            Assert.Empty(fixture.Updates);
            if (!beforeSend)
            { await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Handler.RequestContent!.ReadAsStringAsync()); }
        }
        finally
        {
            caller.Cancel();
            try { await task; } catch { /* Исходная assertion failure приоритетнее уже наблюдённого task failure. */ }
        }
    }

    /// <summary>Caller остаётся приоритетнее deadline при отмене обоих после данных.</summary>
    [Fact]
    public async Task CallerWinsConcurrentDeadlineAfterData()
    {
        using Fixture fixture = new(Partial() + Terminal(), timeout: TimeSpan.FromMilliseconds(200));
        using CancellationTokenSource caller = new();
        async ValueTask Callback(ModelStreamUpdate update, CancellationToken ct)
        {
            caller.Cancel();
            await Task.Delay(300);
            Assert.True(ct.IsCancellationRequested);
        }
        ModelResponse report = (await fixture.Generate(Callback, caller.Token)).Data!;
        Assert.Equal(ModelResponseStatus.Canceled, report.Status);
        Assert.Equal(2, report.Output.Count);
        await AssertDisposed(fixture);
    }

    /// <summary>Deadline на send даёт typed Timeout, app-owned/unexplained OCE не подменяется.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendDeadlineAndUnexplainedCancellation(bool deadline)
    {
        using Fixture fixture = new("", timeout: TimeSpan.FromMilliseconds(200));
        OperationCanceledException unexplained = new();
        fixture.Handler.Respond = async ct =>
        {
            if (deadline) { await Task.Delay(Timeout.Infinite, ct); }
            throw unexplained;
        };
        if (deadline) { Assert.Equal(ServiceErrorType.Timeout, (await fixture.Generate()).Error!.Type); }
        else { Assert.Same(unexplained, await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Generate())); }
        Assert.Empty(fixture.Updates);
        Assert.Equal(1, fixture.Handler.Calls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Handler.RequestContent!.ReadAsStringAsync());
    }

    /// <summary>Некорректные UTF-8 bytes не заменяются молча U+FFFD; known state остаётся в Failed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidUtf8IsRejectedWithoutLosingState(bool partial)
    {
        using Fixture fixture = new((partial ? Partial() : "") + "data: invalid", 1);
        fixture.Stream.CorruptLastByte();
        ServiceResult<ModelResponse> result = await fixture.Generate();
        Assert.Equal(ServiceErrorType.Rejected, partial ? result.Data!.Error!.Type : result.Error!.Type);
        if (partial) { Assert.Equal(2, result.Data!.Output.Count); Assert.Equal(ModelResponseStatus.Failed, result.Data.Status); }
        await AssertDisposed(fixture);
    }

    /// <summary>Начальный BOM допустим даже fragmented; terminal непустой output заменяет старые items целиком.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4096)]
    public async Task BomAndAuthoritativeTerminalOutput(int fragment)
    {
        string body = "\uFEFF" + Partial()
            + Event("{\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[{\"type\":\"future\",\"opaque\":\"authoritative\"}]}}");
        using Fixture fixture = new(body, fragment);
        ModelResponse report = (await fixture.Generate()).Data!;
        Assert.Equal(ModelResponseStatus.Completed, report.Status);
        Assert.Equal("authoritative", Assert.Single(report.Output).Content.GetProperty("opaque").GetString());
        Assert.Single(fixture.Updates);
        await AssertDisposed(fixture);
    }

    /// <summary>Fragmentation UTF-8/CRLF/multiline/comments не влияет на canonical items/order и transport controls.</summary>
    [Theory]
    [InlineData(1, "\n")]
    [InlineData(2, "\r\n")]
    [InlineData(7, "\r")]
    [InlineData(4096, "\n")]
    public async Task CompletedFragmentedMultilineToolsAndOpaque(int fragment, string newline)
    {
        string opaque = """{"type":"reasoning","encrypted_content":"opaque","unknown":{"text":"Привет 🌍"}}""";
        string tool = TOOL.Replace("\"arguments\":\"\"", "\"arguments\":\"{}\"", StringComparison.Ordinal);
        string body = ": keepalive\nid: ignored\nretry: 5\n\n"
            + Event("{\"type\":\"response.output_item.done\",\"output_index\":1,\"item\":" + opaque + "}")
            + Event("{\"type\":\"response.output_item.done\",\"output_index\":0,\"item\":" + tool + "}")
            + "event: response.completed\ndata: {\"response\":{\"status\":\"completed\",\"id\":\"resp-sse\",\n"
            + "data: \"output\":[],\"model\":\"server\",\"usage\":{\"input_tokens\":4},\"future\":true}}\n\n"
            + "data: malicious trailing payload\n\n";
        using Fixture fixture = new(body.Replace("\n", newline, StringComparison.Ordinal), fragment);
        Assert.IsType<HttpApiClient>(fixture.Scope.ServiceProvider.GetRequiredService<HttpApiClient>());
        fixture.Handler.TurnState = "opaque-turn";
        ServiceResult<ModelResponse> result = await fixture.Generate();
        Assert.True(result.Success);
        ModelResponse report = result.Data!;
        Assert.Equal(ModelResponseStatus.Completed, report.Status);
        Assert.Equal("function_call", report.Output[0].Content.GetProperty("type").GetString());
        Assert.Equal("Привет 🌍", report.Output[1].Content.GetProperty("unknown").GetProperty("text").GetString());
        Assert.Equal(2, report.Output.Count);
        Assert.Equal(2, fixture.Updates.Count);
        Assert.Equal(0, report.Envelope!.Content.GetProperty("output").GetArrayLength());
        Assert.Equal(4, report.Envelope.Content.GetProperty("usage").GetProperty("input_tokens").GetInt32());
        Assert.True(report.Envelope.Content.GetProperty("future").GetBoolean());
        Assert.Equal("resp-sse", report.Continuation!.Content.GetProperty("previous_response_id").GetString());
        Assert.Equal("opaque-turn", report.Continuation.Content.GetProperty("headers").GetProperty("x-codex-turn-state").GetString());
        Assert.True(fixture.Handler.Body.GetProperty("stream").GetBoolean());
        Assert.False(fixture.Handler.Body.GetProperty("store").GetBoolean());
        Assert.Equal("Exact-Model", fixture.Handler.Body.GetProperty("model").GetString());
        Assert.Equal("Exact-Effort", fixture.Handler.Body.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("Bearer " + KEY, fixture.Handler.Authorization);
        Assert.Equal("https://gateway.invalid/prefix/v1/responses", fixture.Handler.Url);
        Assert.Null(fixture.Http.DefaultRequestHeaders.Authorization);
        await AssertDisposed(fixture);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Text/arguments delta остаются в partial canonical output при EOF; [DONE] не completion.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("data: [DONE]\n\n")]
    [InlineData("data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[]}}")]
    public async Task EofKeepsPartialTextArgumentsAndUnknown(string suffix)
    {
        using Fixture fixture = new(Partial() + suffix, 1);
        ServiceResult<ModelResponse> result = await fixture.Generate();
        Assert.Equal(ModelResponseStatus.Incomplete, result.Data!.Status);
        Assert.Equal("{\"id\":", result.Data.Output[0].Content.GetProperty("arguments").GetString());
        Assert.Equal("c1", result.Data.Output[0].Content.GetProperty("call_id").GetString());
        Assert.Equal("Привет", result.Data.Output[1].Content.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.True(result.Data.Output[1].Content.GetProperty("future").GetBoolean());
        Assert.Equal("Привет", Assert.Single(fixture.Updates).TextDelta);
        Assert.Null(result.Data.Envelope);
        await AssertDisposed(fixture);
    }

    /// <summary>Terminal event/status и explicit error определяют исход; visible text не требуется.</summary>
    [Theory]
    [InlineData("response.completed", "completed", ModelResponseStatus.Completed)]
    [InlineData("response.completed", "in_progress", ModelResponseStatus.Incomplete)]
    [InlineData("response.completed", "cancelled", ModelResponseStatus.Incomplete)]
    [InlineData("response.incomplete", "incomplete", ModelResponseStatus.Incomplete)]
    [InlineData("response.failed", "failed", ModelResponseStatus.Failed)]
    public async Task TerminalLifecyclePreservesCollectedOutput(string type, string status, ModelResponseStatus expected)
    {
        using Fixture fixture = new(Added() + Event("{\"type\":\"" + type + "\",\"response\":{\"id\":\"resp-final\",\"status\":\""
            + status + "\",\"output\":[],\"incomplete_details\":{\"reason\":\"max_output_tokens\"},\"future\":true}}"));
        ServiceResult<ModelResponse> result = await fixture.Generate();
        Assert.Equal(expected, result.Data!.Status);
        Assert.Single(result.Data.Output);
        Assert.True(result.Data.Envelope!.Content.GetProperty("future").GetBoolean());
        Assert.Equal("resp-final", result.Data.Continuation!.Content.GetProperty("previous_response_id").GetString());
        if (expected == ModelResponseStatus.Failed) { Assert.Equal(ServiceErrorType.Rejected, result.Data.Error!.Type); }
        await AssertDisposed(fixture);
    }

    /// <summary>Empty stream, unknown lifecycle и missing terminal envelope/output не дают Completed.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("data: [DONE]\n\n")]
    [InlineData("data: {\"type\":\"response.created\",\"response\":{\"status\":\"completed\",\"output\":[]}}\n\n")]
    [InlineData("data: {\"type\":\"response.completed\"}\n\n")]
    [InlineData("data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n")]
    [InlineData("data: {\"type\":\"response.future\",\"private\":\"synthetic-private-sse\"}\n\n")]
    public async Task NoTerminalConfirmationIsIncomplete(string body)
    {
        using Fixture fixture = new(body, 1);
        ServiceResult<ModelResponse> result = await fixture.Generate();
        Assert.True(result.Success);
        Assert.Equal(ModelResponseStatus.Incomplete, result.Data!.Status);
        Assert.Empty(fixture.Updates);
        await AssertDisposed(fixture);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Explicit error до/после items сохраняет typed failure и raw private fields только в envelope.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ErrorEventsBeforeAndAfterData(bool partial, bool nested)
    {
        string error = "{\"type\":\"server_error\",\"code\":\"server_error\",\"param\":\"input\",\"message\":\"" + PRIVATE + "\"}";
        string body = nested ? "{\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[],\"error\":" + error + "}}"
            : "{\"type\":\"error\",\"error\":" + error + ",\"future\":true}";
        using Fixture fixture = new((partial ? Partial() : "") + Event(body));
        ModelResponse report = (await fixture.Generate()).Data!;
        Assert.Equal(ModelResponseStatus.Failed, report.Status);
        Assert.Equal(partial ? 2 : 0, report.Output.Count);
        CodexLbServiceError failure = Assert.IsType<CodexLbServiceError>(report.Error);
        Assert.Equal("server_error", failure.Code);
        Assert.Equal("input", failure.Param);
        Assert.Contains(PRIVATE, report.Envelope!.Content.GetRawText());
        AssertSafe(failure.Message + fixture.Log.Text);
        await AssertDisposed(fixture);
    }

    /// <summary>Ошибка JSON до/после data не теряет partial state; secrets не выходят в public error/logger.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidJsonKeepsKnownOutput(bool partial)
    {
        using Fixture fixture = new((partial ? Partial() : "") + Event("invalid " + PRIVATE));
        ServiceResult<ModelResponse> result = await fixture.Generate();
        ServiceError error;
        if (partial)
        {
            Assert.True(result.Success);
            Assert.Equal(ModelResponseStatus.Failed, result.Data!.Status);
            Assert.Equal(2, result.Data.Output.Count);
            error = result.Data.Error!;
        }
        else { Assert.False(result.Success); error = result.Error!; }
        Assert.Equal(ServiceErrorType.Rejected, error.Type);
        AssertSafe(error.Message + fixture.Log.Text);
        await AssertDisposed(fixture);
    }

    /// <summary>Дельты не выдумывают item и не принимают иной item_id.</summary>
    [Theory]
    [InlineData("{\"type\":\"response.output_text.delta\",\"output_index\":8,\"content_index\":0,\"delta\":\"x\"}")]
    [InlineData("{\"type\":\"response.function_call_arguments.delta\",\"output_index\":0,\"item_id\":\"other\",\"delta\":\"x\"}")]
    [InlineData("{\"type\":\"response.output_item.done\",\"output_index\":-1,\"item\":{}}")]
    public async Task InvalidLinksFailWithPreviousData(string data)
    {
        using Fixture fixture = new(Added() + Event(data));
        ModelResponse report = (await fixture.Generate()).Data!;
        Assert.Equal(ModelResponseStatus.Failed, report.Status);
        Assert.Single(report.Output);
        Assert.Equal(ServiceErrorType.Rejected, report.Error!.Type);
        await AssertDisposed(fixture);
    }

    /// <summary>Callbacks строго awaited; следующая delta и возврат ждут разрешения первой.</summary>
    [Fact]
    public async Task CallbacksAreSequentialAndAwaited()
    {
        using Fixture fixture = new(Partial() + Event("{\"type\":\"response.output_text.delta\",\"output_index\":1,\"content_index\":0,\"delta\":\"!\"}")
            + Terminal());
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource caller = new();
        int count = 0;
        int active = 0;
        async ValueTask Callback(ModelStreamUpdate update, CancellationToken ct)
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            int current = Interlocked.Increment(ref count);
            if (current == 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
            Interlocked.Decrement(ref active);
        }
        Task<ServiceResult<ModelResponse>> task = fixture.Generate(Callback, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, count);
            Assert.False(task.IsCompleted);
            release.SetResult();
            Assert.Equal(ModelResponseStatus.Completed, (await task).Data!.Status);
            Assert.Equal(2, count);
            Assert.Equal(0, active);
            await AssertDisposed(fixture);
            Assert.Equal(2, count);
        }
        finally
        {
            release.TrySetResult();
            caller.Cancel();
            try { await task; } catch { /* Cleanup наблюдает task, не заменяя исходную assertion failure. */ }
        }
    }

    /// <summary>Callback exceptions любого adapter-caught типа распространяются тем же объектом.</summary>
    [Theory]
    [InlineData("json")]
    [InlineData("http")]
    [InlineData("cancel")]
    [InlineData("decoder")]
    [InlineData("other")]
    public async Task CallbackExceptionsAreNotServerFailures(string kind)
    {
        using Fixture fixture = new(Partial() + Terminal());
        using CancellationTokenSource caller = new();
        Exception failure = kind switch
        {
            "json" => new JsonException(PRIVATE),
            "http" => new HttpRequestFailedException(new("https://private.invalid"), HttpStatusCode.BadRequest, PRIVATE, PRIVATE),
            "cancel" => new OperationCanceledException(caller.Token),
            "decoder" => new DecoderFallbackException(PRIVATE),
            _ => new InvalidOperationException(PRIVATE)
        };
        int count = 0;
        ValueTask Callback(ModelStreamUpdate update, CancellationToken ct)
        {
            count++;
            if (kind == "cancel") { caller.Cancel(); }
            return ValueTask.FromException(failure);
        }
        Exception actual = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Generate(Callback, caller.Token));
        Assert.Same(failure, actual);
        Assert.Equal(1, count);
        await AssertDisposed(fixture);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Caller/deadline до/после data дают исходный token либо typed timeout с partial output.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CancellationAndDeadlineWhileReading(bool partial, bool timeout)
    {
        using Fixture fixture = new(partial ? Partial() : "", timeout: timeout ? TimeSpan.FromMilliseconds(250) : null);
        fixture.Stream.BlockAtEnd = true;
        using CancellationTokenSource caller = new();
        Task<ServiceResult<ModelResponse>> task = fixture.Generate(ct: caller.Token);
        try
        {
            await fixture.Stream.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (!timeout) { caller.Cancel(); }
            if (!partial && !timeout)
            {
                OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
                Assert.Equal(caller.Token, failure.CancellationToken);
            }
            else
            {
                ServiceResult<ModelResponse> result = await task;
                if (!partial) { Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type); }
                else
                {
                    Assert.Equal(timeout ? ModelResponseStatus.Failed : ModelResponseStatus.Canceled, result.Data!.Status);
                    Assert.Equal(2, result.Data.Output.Count);
                    if (timeout) { Assert.Equal(ServiceErrorType.Timeout, result.Data.Error!.Type); }
                }
            }
            await AssertDisposed(fixture);
        }
        finally
        {
            caller.Cancel();
            try { await task; } catch { /* Cleanup наблюдает task, не заменяя исходную assertion failure. */ }
        }
    }

    /// <summary>Поздняя отмена после terminal/disposal сохраняет report; explicit failure приоритетнее.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCancellationKeepsReportAndFailurePriority(bool failed)
    {
        using Fixture fixture = new(Partial() + (failed ? Event("{\"type\":\"response.failed\",\"response\":{\"status\":\"failed\",\"output\":[]}}") : Terminal()));
        using CancellationTokenSource caller = new();
        fixture.Stream.OnDispose = caller.Cancel;
        ModelResponse report = (await fixture.Generate(ct: caller.Token)).Data!;
        Assert.Equal(failed ? ModelResponseStatus.Failed : ModelResponseStatus.Canceled, report.Status);
        Assert.Equal(2, report.Output.Count);
        Assert.NotNull(report.Envelope);
        await AssertDisposed(fixture);
    }

    /// <summary>HTTP failure использует actual safe error pipeline и не вызывает callbacks/retry.</summary>
    [Theory]
    [InlineData(401, ServiceErrorType.Unauthorized)]
    [InlineData(429, ServiceErrorType.Rejected)]
    public async Task HttpFailureUsesSafeStage14Rules(int status, ServiceErrorType type)
    {
        using Fixture fixture = new("{\"error\":{\"type\":\"server_error\",\"message\":\"" + PRIVATE + "\"}}");
        fixture.Handler.Status = (HttpStatusCode)status;
        fixture.Handler.MediaType = "application/json";
        ServiceResult<ModelResponse> result = await fixture.Generate();
        Assert.Equal(type, result.Error!.Type);
        Assert.Empty(fixture.Updates);
        AssertSafe(result.Error.Message + fixture.Log.Text);
        await AssertDisposed(fixture);
    }

    /// <summary>Unexpected I/O не становится lifecycle/HTTP failure и освобождает response.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedIoPropagates(bool partial)
    {
        using Fixture fixture = new(partial ? Partial() : "");
        IOException failure = new(PRIVATE);
        fixture.Stream.EndFailure = failure;
        Exception actual = await Assert.ThrowsAsync<IOException>(() => fixture.Generate());
        Assert.Same(failure, actual);
        await AssertDisposed(fixture);
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>SSE и JSON используют один binding; новый отсутствующий id не возвращает старый anchor.</summary>
    [Fact]
    public async Task ContinuationCrossTransportAndMissingId()
    {
        using Fixture fixture = new(Terminal());
        ModelResponse first = (await fixture.Generate()).Data!;
        fixture.Handler.Use(new(Event("{\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[]}}"), 1));
        ModelResponse second = (await fixture.Generate(request: Request(first.Continuation))).Data!;
        Assert.Equal("resp-terminal", fixture.Handler.Body.GetProperty("previous_response_id").GetString());
        Assert.False(second.Continuation!.Content.TryGetProperty("previous_response_id", out _));
        fixture.Handler.Use(new("{\"id\":\"resp-json\",\"status\":\"completed\",\"output\":[]}", 1));
        fixture.Handler.MediaType = "application/json";
        ModelAccess access = (await fixture.Scope.ServiceProvider.GetRequiredService<IModelAccessResolver>().ResolveAsync(fixture.Call.OwnerId)).Data!;
        ModelResponse json = (await fixture.Gateway.GenerateAsync(fixture.Call, Request(second.Continuation), access)).Data!;
        Assert.False(fixture.Handler.Body.GetProperty("stream").GetBoolean());
        Assert.Equal(ModelResponseStatus.Completed, json.Status);
        Assert.False(fixture.Handler.Body.TryGetProperty("previous_response_id", out _));
        int calls = fixture.Handler.Calls;
        ServiceResult<ModelResponse> mismatch = await fixture.Gateway.GenerateAsync(fixture.Call, Request(json.Continuation), new("other-key"), (_, _) => ValueTask.CompletedTask);
        Assert.Equal(ServiceErrorType.Conflict, mismatch.Error!.Type);
        Assert.Equal(calls, fixture.Handler.Calls);
    }

    /// <summary>Создаёт закрытый SSE data event.</summary>
    private static string Event(string data) => "data: " + data + "\n\n";
    /// <summary>Создаёт indexed tool event с unknown полями.</summary>
    private static string Added() => Event(ADDED.Replace("TOOL", TOOL, StringComparison.Ordinal));
    /// <summary>Создаёт partial tools/text без terminal.</summary>
    private static string Partial() => Added()
        + Event("{\"type\":\"response.function_call_arguments.delta\",\"output_index\":0,\"item_id\":\"f1\",\"delta\":\"{\\\"id\\\":\"}")
        + Event("{\"type\":\"response.output_item.added\",\"output_index\":1,\"item\":{\"id\":\"m1\",\"type\":\"message\",\"role\":\"assistant\",\"content\":[],\"future\":true}}")
        + Event("{\"type\":\"response.content_part.added\",\"output_index\":1,\"content_index\":0,\"part\":{\"type\":\"output_text\",\"text\":\"\",\"annotations\":[]}}")
        + Event("{\"type\":\"response.output_text.delta\",\"output_index\":1,\"content_index\":0,\"delta\":\"Привет\"}");
    /// <summary>Создаёт подтверждённый terminal с пустым массивом для backfill items.</summary>
    private static string Terminal() => Event("{\"type\":\"response.completed\",\"response\":{\"id\":\"resp-terminal\",\"status\":\"completed\",\"output\":[]}}");
    /// <summary>Создаёт exact request.</summary>
    private static ModelRequest Request(ModelContinuation? continuation = null) => new("Exact-Model", "Exact-Effort", "instructions", [], [], continuation);
    /// <summary>Проверяет освобождение stream/response/request и отсутствие retry.</summary>
    private static async Task AssertDisposed(Fixture fixture)
    {
        Assert.True(fixture.Stream.Disposed);
        Assert.True(fixture.Handler.Content!.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Handler.RequestContent!.ReadAsStringAsync());
    }
    /// <summary>Проверяет formatted/structured log и public messages.</summary>
    private static void AssertSafe(string text)
    {
        Assert.DoesNotContain(KEY, text);
        Assert.DoesNotContain(PRIVATE, text);
        Assert.DoesNotContain("gateway.invalid", text);
    }

    /// <summary>Public adapter/resolver/actual library с app-owned HttpClient без hosting.</summary>
    private class Fixture : IDisposable
    {
        internal readonly Handler Handler;
        internal readonly LogProvider Log = new();
        internal readonly HttpClient Http;
        internal readonly ServiceProvider Root;
        internal readonly IServiceScope Scope;
        internal readonly List<ModelStreamUpdate> Updates = [];
        internal FragmentedStream Stream => Handler.Stream;
        internal ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        internal IModelGateway Gateway => Scope.ServiceProvider.GetRequiredService<IModelGateway>();
        /// <summary>Регистрирует actual transport через public DI без side effects.</summary>
        internal Fixture(string body, int fragment = 3, TimeSpan? timeout = null)
        {
            Handler = new(new(body, fragment));
            Http = new(Handler) { Timeout = Timeout.InfiniteTimeSpan };
            ServiceCollection services = new();
            services.AddLogging(builder => builder.AddProvider(Log));
            services.AddCodexLbConfiguration(options =>
            {
                options.BaseAddress = "https://gateway.invalid/prefix/";
                options.Model = "Exact-Model";
                options.GenerationTimeout = timeout ?? TimeSpan.FromSeconds(5);
            });
            services.AddScoped<IIndividualModelKeySource, KeySource>();
            services.AddCodexLbResponses(_ => Http, new() { ErrorContentMode = HttpErrorContentLogMode.JsonStructure });
            Root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Scope = Root.CreateScope();
            Assert.Equal(0, Handler.Calls);
        }
        /// <summary>Выбирает per-call key публичным resolver и вызывает gateway.</summary>
        internal async Task<ServiceResult<ModelResponse>> Generate(Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null,
            CancellationToken ct = default, ModelRequest? request = null)
        {
            ModelAccess access = (await Scope.ServiceProvider.GetRequiredService<IModelAccessResolver>().ResolveAsync(Call.OwnerId, ct)).Data!;
            return await Gateway.GenerateAsync(Call, request ?? Request(), access, callback ?? Collect, ct);
        }
        /// <summary>Сохраняет независимые updates до возврата.</summary>
        private ValueTask Collect(ModelStreamUpdate update, CancellationToken ct) { Updates.Add(update); return ValueTask.CompletedTask; }
        /// <inheritdoc/>
        public void Dispose() { Scope.Dispose(); Root.Dispose(); Http.Dispose(); }
    }
    /// <inheritdoc/>
    private class KeySource : IIndividualModelKeySource
    {
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(KEY);
    }
    /// <summary>Локальный fake HTTP с actual request serialization/response wrapper.</summary>
    private class Handler(FragmentedStream stream) : HttpMessageHandler
    {
        internal FragmentedStream Stream = stream;
        internal int Calls;
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal string MediaType = "text/event-stream";
        internal string? TurnState;
        internal JsonElement Body;
        internal string? Authorization;
        internal string? Url;
        internal HttpContent? RequestContent;
        internal TrackingContent? Content;
        internal Func<CancellationToken, Task<HttpResponseMessage>>? Respond;
        /// <summary>Заменяет stream для следующего public вызова.</summary>
        internal void Use(FragmentedStream next) => Stream = next;
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            RequestContent = request.Content;
            using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Body = document.RootElement.Clone();
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            if (Respond is not null) { return await Respond(ct); }
            Content = new(Stream);
            Content.Headers.ContentType = new(MediaType);
            HttpResponseMessage response = new(Status) { Content = Content, ReasonPhrase = PRIVATE };
            response.Headers.Add("x-private", PRIVATE);
            if (TurnState is not null) { response.Headers.Add("x-codex-turn-state", TurnState); }
            return response;
        }
    }
    /// <summary>Проверяет освобождение HTTP response content.</summary>
    private class TrackingContent(Stream stream) : StreamContent(stream)
    {
        internal bool Disposed;
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    /// <summary>Локальный fragmented stream с детерминированным EOF/I/O/cancellation и disposal.</summary>
    private class FragmentedStream(string text, int fragment) : Stream
    {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(text);
        private int position;
        internal bool BlockAtEnd;
        internal bool Disposed;
        internal Exception? EndFailure;
        internal Action? OnDispose;
        internal readonly TaskCompletionSource Blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Вносит неверный UTF-8 byte только в локальную fixture.</summary>
        internal void CorruptLastByte() => bytes[^1] = 0xFF;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (position < bytes.Length)
            {
                int count = Math.Min(Math.Min(fragment, buffer.Length), bytes.Length - position);
                bytes.AsMemory(position, count).CopyTo(buffer);
                position += count;
                return count;
            }
            if (EndFailure is not null) { throw EndFailure; }
            if (BlockAtEnd) { Blocked.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!Disposed) { Disposed = true; OnDispose?.Invoke(); }
            base.Dispose(disposing);
        }
    }
    /// <inheritdoc/>
    private class LogProvider : ILoggerProvider
    {
        internal string Text = "";
        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => new Logger(this);
        /// <inheritdoc/>
        public void Dispose() { }
        /// <inheritdoc/>
        private class Logger(LogProvider provider) : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            /// <inheritdoc/>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Assert.Null(exception);
                provider.Text += formatter(state, exception) + JsonSerializer.Serialize(state);
            }
        }
    }
}
