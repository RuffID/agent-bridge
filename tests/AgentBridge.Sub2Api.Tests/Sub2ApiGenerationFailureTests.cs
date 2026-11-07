using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using HttpClientLibrary.Exceptions;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiGenerationFixture;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Один send сохраняет cancellation/deadline/partial output и primary/cleanup failures.</summary>
public class Sub2ApiGenerationFailureTests
{
    /// <summary>Caller, отменённый внутри JSON callback, не теряет уже прочитанный полный отчёт.</summary>
    [Fact]
    public async Task JsonCallbackCallerPreservesFullOutput()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        using CancellationTokenSource caller = new();
        fixture.Reply(COMPLETED, "application/json");
        int callbacks = 0;

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            (_, _) => { callbacks++; caller.Cancel(); return ValueTask.CompletedTask; }, caller.Token);

        // Assert
        Assert.Equal(1, callbacks);
        Assert.Equal(ModelResponseStatus.Canceled, result.Data!.Status);
        Assert.Equal(2, result.Data.Output.Count);
        Assert.NotNull(result.Data.Envelope);
    }

    /// <summary>Invalid UTF-8 даже в unknown поле отклоняется; SSE partial остаётся в Failed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidUtf8CannotHideInUnknownField(bool sse)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        string text = """{"status":"completed","output":[],"unknown":"X"}""";
        string mediaType = "application/json";
        if (sse)
        {
            text = Partial() + Frame("""{"type":"unknown","future":"X"}""");
            mediaType = "text/event-stream";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        bytes[Array.LastIndexOf(bytes, (byte)'X')] = 0xFF;
        // Фрагмент заканчивается перед повреждением, чтобы partial frame был обработан до decoder failure.
        Sub2ApiTestStream stream = new(bytes, 1);
        fixture.Handler.Respond = (_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentType = new(mediaType);
            return Task.FromResult(response);
        };

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        if (sse)
        {
            Assert.Equal(ModelResponseStatus.Failed, result.Data!.Status);
            Assert.NotEmpty(result.Data.Output);
            Assert.Equal(ServiceErrorType.Rejected, result.Data.Error!.Type);
        }
        else
        {
            Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        }

        Assert.True(stream.Disposed);
    }

    /// <summary>Неожиданный read и standalone cleanup сохраняют исходный объект/stack без safe masking.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedReadOrCleanupKeepsIdentity(bool cleanupOnly)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Exception primary = new IOException("private-read");
        Sub2ApiTestStream stream = fixture.Reply(Partial());
        if (cleanupOnly)
        {
            stream.CleanupFailure = primary;
        }
        else
        {
            stream.ReadFailure = primary;
            stream.CleanupFailure = new JsonException("private-cleanup");
        }

        // Act
        Exception? observed = await Record.ExceptionAsync(() => fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(primary, observed);
        Assert.Contains(nameof(Sub2ApiTestStream), primary.StackTrace);
        if (!cleanupOnly)
        {
            Assert.Contains(stream.CleanupFailure, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(primary.Data["HttpClientLibrary.CleanupExceptions"]));
        }

        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Deadline действует до headers, не создавая второй HTTP или смену ключа.</summary>
    [Fact]
    public async Task DeadlineBeforeHeadersIsTimeout()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new(timeout: TimeSpan.FromSeconds(2));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (_, ct) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException();
        };

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(entered.Task.IsCompletedSuccessfully);
        Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>HTTP отказы обоих envelope видов маппятся безопасно и не повторяются.</summary>
    [Theory]
    [InlineData(401, false, ServiceErrorType.Unauthorized)]
    [InlineData(403, true, ServiceErrorType.Forbidden)]
    [InlineData(400, false, ServiceErrorType.Validation)]
    [InlineData(429, true, ServiceErrorType.Rejected)]
    [InlineData(503, false, ServiceErrorType.Rejected)]
    public async Task HttpFailureDoesNotRetry(int status, bool flat, ServiceErrorType expected)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        string json = """{"error":{"message":"private-body","code":"secret"}}""";
        if (flat)
        {
            json = """{"error":"private-body","message":"secret"}""";
        }

        Sub2ApiTestStream stream = fixture.Reply(json, "application/json", status: (HttpStatusCode)status);

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.True(stream.Disposed);
        Assert.DoesNotContain("private-body", result.Error.Message + string.Join("", fixture.Logs.Entries));
        Assert.DoesNotContain("secret", result.Error.Message + string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Callbacks awaited последовательно; завершение gateway ждёт release и terminal.</summary>
    [Fact]
    public async Task CallbacksAreAwaited()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        string secondDelta = Frame("""{"type":"response.output_text.delta","output_index":0,"item_id":"m","content_index":0,"delta":" world"}""");
        Sub2ApiTestStream stream = fixture.Reply(Partial() + secondDelta + Terminal());
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        async ValueTask Callback(ModelStreamUpdate update, CancellationToken ct)
        {
            calls++;
            if (calls == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(ct);
            }
        }

        // Act
        Task<ServiceResult<ModelResponse>> pending = fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            Callback, TestContext.Current.CancellationToken);
        ServiceResult<ModelResponse>? result = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(pending.IsCompleted);
            Assert.False(stream.Disposed);
            Assert.Equal(1, calls);

            // Act
            release.SetResult();
            result = await pending;
        }
        finally
        {
            release.TrySetResult();
            await Record.ExceptionAsync(() => pending);
        }

        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(ModelResponseStatus.Completed, result!.Data!.Status);
        Assert.True(stream.Disposed);
    }

    /// <summary>Callback exceptions сохраняют identity, даже совпадая с transport catch типами.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CallbackPrimaryAndCleanupRemainObservable(bool canceled, bool unary)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiTestStream stream;
        if (unary)
        {
            stream = fixture.Reply(COMPLETED, "application/json");
        }
        else
        {
            stream = fixture.Reply(Partial() + Terminal());
        }

        Exception primary = new JsonException("private-callback");
        if (canceled)
        {
            primary = new OperationCanceledException();
        }

        IOException cleanup = new("private-cleanup");
        stream.CleanupFailure = cleanup;

        // Act
        Exception? observed = await Record.ExceptionAsync(() => fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            (_, _) => throw primary, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(primary, observed);
        Assert.Contains(cleanup, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(primary.Data["HttpClientLibrary.CleanupExceptions"]));
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.DoesNotContain("private-callback", string.Join("", fixture.Logs.Entries));
        Assert.DoesNotContain("private-cleanup", string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Caller и deadline различаются при блокировке до данных и после partial output.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ReadingCancellationPreservesPartialAndToken(bool partial, bool timeout)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new(timeout: TimeSpan.FromSeconds(2));
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string text = "";
        if (partial)
        {
            text = Partial();
        }

        Sub2ApiTestStream stream = fixture.Reply(text);
        stream.BlockAtEnd = true;
        ServiceResult<ModelResponse>? result = null;
        Exception? observed = null;

        // Act
        Task<ServiceResult<ModelResponse>> pending = fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: caller.Token);
        try
        {
            await stream.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (!timeout)
            {
                caller.Cancel();
            }

            observed = await Record.ExceptionAsync(async () => result = await pending);
        }
        finally
        {
            caller.Cancel();
            await Record.ExceptionAsync(() => pending);
        }

        // Assert
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.True(stream.Disposed);
        if (partial)
        {
            Assert.Null(observed);
            Assert.NotEmpty(result!.Data!.Output);
            if (timeout)
            {
                Assert.Equal(ModelResponseStatus.Failed, result.Data.Status);
                Assert.Equal(ServiceErrorType.Timeout, result.Data.Error!.Type);
            }
            else
            {
                Assert.Equal(ModelResponseStatus.Canceled, result.Data.Status);
            }
        }
        else if (timeout)
        {
            Assert.Null(observed);
            Assert.Equal(ServiceErrorType.Timeout, result!.Error!.Type);
        }
        else
        {
            Assert.Equal(caller.Token, Assert.IsAssignableFrom<OperationCanceledException>(observed).CancellationToken);
        }
    }

    /// <summary>После полного отчёта caller сохраняет output; explicit model failure приоритетнее.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task LateCallerPreservesReport(bool failed, bool unary)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        using CancellationTokenSource caller = new();
        string text = Terminal();
        string mediaType = "text/event-stream";
        if (failed)
        {
            text = Partial() + Frame("""{"type":"error","message":"private"}""");
        }

        if (unary)
        {
            mediaType = "application/json";
            text = COMPLETED;
            if (failed)
            {
                text = COMPLETED.Replace("completed", "failed", StringComparison.Ordinal);
            }
        }

        Sub2ApiTestStream stream = fixture.Reply(text, mediaType);
        stream.OnDispose = caller.Cancel;

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, fixture.Request, fixture.Access,
            cancellationToken: caller.Token);

        // Assert
        Assert.NotEmpty(result.Data!.Output);
        Assert.Null(result.Data.Continuation);
        if (failed)
        {
            Assert.Equal(ModelResponseStatus.Failed, result.Data.Status);
        }
        else
        {
            Assert.Equal(ModelResponseStatus.Canceled, result.Data.Status);
        }
    }

    /// <summary>Malformed SSE сохраняет partial output; raw read/cleanup primary не маскируются.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedPartialAndCleanup(bool cleanupFails)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiTestStream stream = fixture.Reply(Partial() + Frame("{invalid"));
        IOException cleanup = new("private-cleanup");
        if (cleanupFails)
        {
            stream.CleanupFailure = cleanup;
        }

        ServiceResult<ModelResponse>? result = null;

        // Act
        Exception? observed = await Record.ExceptionAsync(async () => result = await fixture.Gateway.GenerateAsync(
            fixture.Call, fixture.Request, fixture.Access, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        if (cleanupFails)
        {
            JsonException primary = Assert.IsAssignableFrom<JsonException>(observed);
            Assert.Contains(cleanup, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(primary.Data["HttpClientLibrary.CleanupExceptions"]));
        }
        else
        {
            Assert.Null(observed);
            Assert.Equal(ModelResponseStatus.Failed, result!.Data!.Status);
            Assert.NotEmpty(result.Data.Output);
            Assert.Equal(ServiceErrorType.Rejected, result.Data.Error!.Type);
        }

        Assert.True(stream.Disposed);
    }

    /// <summary>HTTP error processing/cleanup failures сохраняют status и причины actual library.</summary>
    [Fact]
    public async Task ErrorBodyCleanupIsNotSwallowed()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiTestStream stream = fixture.Reply("{}", "application/json", status: HttpStatusCode.Unauthorized);
        IOException cleanup = new("private-cleanup");
        stream.CleanupFailure = cleanup;

        // Act
        HttpRequestFailedException observed = await Assert.ThrowsAsync<HttpRequestFailedException>(() => fixture.Gateway.GenerateAsync(
            fixture.Call, fixture.Request, fixture.Access, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, observed.StatusCode);
        Assert.Contains(cleanup, observed.ProcessingFailure!.CleanupExceptions);
        Assert.Equal(1, fixture.Handler.Calls);
    }
}
