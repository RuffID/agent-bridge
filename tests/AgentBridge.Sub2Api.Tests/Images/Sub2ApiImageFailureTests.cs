using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using HttpClientLibrary.Exceptions;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiCatalogFixture;
using static AgentBridge.Sub2Api.Tests.Sub2ApiUsageTests;
using static AgentBridge.Sub2Api.Tests.Sub2ApiImageTests;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Отмена, deadline, no retry и cleanup actual Images transport без внешних ресурсов.</summary>
public class Sub2ApiImageFailureTests
{
    /// <summary>Ранняя отмена и некорректный ключ не достигают factory у generation/edit.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LocalAccessFailureBeforeFactory(bool edit, bool canceled)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string key = "invalid key";
        if (canceled)
        {
            key = INDIVIDUAL_KEY;
            caller.Cancel();
        }

        IModelAuxiliaryGateway gateway = Gateway(fixture);
        ServiceResult<ModelAuxiliaryResult>? result = null;

        // Act
        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            if (edit)
            {
                result = await gateway.EditImageAsync(new() { Model = "image-model", Prompt = PRIVATE,
                    Images = [new() { FileName = "x.png", Bytes = [1] }] }, new(key), caller.Token);
            }
            else
            {
                result = await gateway.GenerateImageAsync(new() { Model = "image-model", Prompt = PRIVATE }, new(key), caller.Token);
            }
        });

        // Assert
        if (canceled)
        {
            Assert.Equal(caller.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        }
        else
        {
            Assert.Null(failure);
            Assert.Equal(ServiceErrorType.Validation, result!.Error!.Type);
        }

        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Caller отмена на send/success/error body сохраняет токен и освобождает request/response content.</summary>
    [Theory]
    [MemberData(nameof(Stages))]
    public async Task CallerCancellationAtStage(bool edit, int status)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Sub2ApiTestStream stream = new([], 1) { BlockAtEnd = true };
        TaskCompletionSource send = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpContent? requestContent = null;
        ConfigureStage(fixture, stream, send, status, content => requestContent = content);
        Task entered = stream.Blocked.Task;
        if (status == 0)
        {
            entered = send.Task;
        }

        // Act
        Task<ServiceResult<ModelAuxiliaryResult>> pending = SendAsync(fixture, edit, caller.Token);
        OperationCanceledException? failure = null;
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            caller.Cancel();
            failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally
        {
            caller.Cancel();
            await Record.ExceptionAsync(() => pending);
        }

        // Assert
        Assert.Equal(caller.Token, failure!.CancellationToken);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(status != 0, stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => requestContent!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        AssertSafe(fixture, string.Empty);
        Assert.Contains("Outcome=Canceled", fixture.Logs.Entries);
        Assert.DoesNotContain("Outcome=Completed", fixture.Logs.Entries);
    }

    /// <summary>Один deadline охватывает send и чтение success/error body без повторной платной операции.</summary>
    [Theory]
    [MemberData(nameof(Stages))]
    public async Task DeadlineAtStage(bool edit, int status)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(timeout: TimeSpan.FromSeconds(2), auxiliary: true);
        Sub2ApiTestStream stream = new([], 1) { BlockAtEnd = true };
        TaskCompletionSource send = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpContent? requestContent = null;
        ConfigureStage(fixture, stream, send, status, content => requestContent = content);
        _ = Gateway(fixture);
        Task entered = stream.Blocked.Task;
        if (status == 0)
        {
            entered = send.Task;
        }

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await SendAsync(fixture, edit, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(entered.IsCompletedSuccessfully);
        Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(status != 0, stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => requestContent!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Одновременный caller/deadline сохраняет caller, более короткий HttpClient timeout даёт Timeout.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TimeoutAndCallerPriority(bool edit, bool callerWins)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(timeout: TimeSpan.FromSeconds(1), auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (!callerWins)
        {
            fixture.Http.Timeout = TimeSpan.FromMilliseconds(250);
        }

        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (_, ct) =>
        {
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                if (callerWins)
                {
                    caller.Cancel();
                }

                throw;
            }

            throw new InvalidOperationException();
        };
        _ = Gateway(fixture);
        ServiceResult<ModelAuxiliaryResult>? result = null;

        // Act
        Exception? failure = await Record.ExceptionAsync(async () => result = await SendAsync(fixture, edit, caller.Token));

        // Assert
        Assert.True(entered.Task.IsCompletedSuccessfully);
        if (callerWins)
        {
            Assert.Equal(caller.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        }
        else
        {
            Assert.Null(failure);
            Assert.Equal(ServiceErrorType.Timeout, result!.Error!.Type);
        }

        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Network failure возвращается безопасным отказом, неожиданный I/O остаётся наблюдаемым.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TransportFailureWithoutRetry(bool edit, bool io)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        Exception expected = new HttpRequestException(PRIVATE);
        if (io)
        {
            expected = new IOException(PRIVATE);
        }

        fixture.Handler.Respond = (_, _) => throw expected;
        ServiceResult<ModelAuxiliaryResult>? result = null;

        // Act
        Exception? failure = await Record.ExceptionAsync(async () => result = await SendAsync(fixture, edit, TestContext.Current.CancellationToken));

        // Assert
        if (io)
        {
            Assert.Same(expected, failure);
        }
        else
        {
            Assert.Null(failure);
            Assert.Equal(ServiceErrorType.Rejected, result!.Error!.Type);
            AssertSafe(fixture, result.Error.Message);
        }

        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Primary/cancellation вместе с cleanup failure сохраняют обе причины и error status.</summary>
    [Theory]
    [MemberData(nameof(CleanupCases))]
    public async Task CombinedCleanupFailureRemainsObservable(bool edit, bool httpError, bool cancel)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IOException cleanup = new(PRIVATE);
        byte[] bytes = Encoding.UTF8.GetBytes("{malformed");
        if (cancel)
        {
            bytes = [];
        }

        Sub2ApiTestStream stream = new(bytes, 2) { BlockAtEnd = cancel, CleanupFailure = cleanup };
        HttpStatusCode status = HttpStatusCode.OK;
        if (httpError)
        {
            status = HttpStatusCode.Forbidden;
        }

        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream, status));

        // Act
        Task<ServiceResult<ModelAuxiliaryResult>> pending = SendAsync(fixture, edit, caller.Token);
        Exception? failure = null;
        try
        {
            if (cancel)
            {
                await stream.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                caller.Cancel();
            }

            failure = await Record.ExceptionAsync(() => pending);
        }
        finally
        {
            caller.Cancel();
            await Record.ExceptionAsync(() => pending);
        }

        // Assert
        if (cancel && httpError)
        {
            HttpResponseCanceledException canceled = Assert.IsType<HttpResponseCanceledException>(failure);
            Assert.Equal(HttpStatusCode.Forbidden, canceled.StatusCode);
            Assert.Contains(cleanup, canceled.ProcessingFailure.CleanupExceptions);
        }
        else if (httpError)
        {
            HttpRequestFailedException http = Assert.IsType<HttpRequestFailedException>(failure);
            Assert.Equal(HttpStatusCode.Forbidden, http.StatusCode);
            Assert.Contains(cleanup, http.ProcessingFailure!.CleanupExceptions);
        }
        else
        {
            if (cancel)
            {
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
            }
            else
            {
                Assert.IsType<JsonException>(failure);
            }

            Assert.Contains(cleanup, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(failure!.Data["HttpClientLibrary.CleanupExceptions"]));
        }

        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Cleanup-only после валидного JSON не теряется в успешном результате.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulBodyCleanupFailureIsObservable(bool edit)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        IOException cleanup = new(PRIVATE);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes(RESULT), 3) { CleanupFailure = cleanup };
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        Exception? failure = await Record.ExceptionAsync(() => SendAsync(fixture, edit, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(cleanup, failure);
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, string.Empty);
        Assert.Contains("Outcome=Exception", fixture.Logs.Entries);
        Assert.DoesNotContain("Outcome=Completed", fixture.Logs.Entries);
    }

    /// <summary>Стадии отмены/deadline для обоих путей.</summary>
    public static IEnumerable<object[]> Stages()
    {
        foreach (bool edit in new[] { false, true })
        {
            foreach (int status in new[] { 0, 200, 503 })
            {
                yield return [edit, status];
            }
        }
    }

    /// <summary>Комбинации primary/cleanup для generation/edit.</summary>
    public static IEnumerable<object[]> CleanupCases()
    {
        foreach (bool edit in new[] { false, true })
        {
            foreach (bool httpError in new[] { false, true })
            {
                foreach (bool cancel in new[] { false, true })
                {
                    yield return [edit, httpError, cancel];
                }
            }
        }
    }

    /// <summary>Настраивает gate send/body и наблюдение content без реальных запросов.</summary>
    private static void ConfigureStage(Sub2ApiCatalogFixture fixture, Sub2ApiTestStream stream,
        TaskCompletionSource send, int status, Action<HttpContent> capture)
    {
        fixture.Handler.Respond = async (request, ct) =>
        {
            capture(request.Content!);
            if (status == 0)
            {
                send.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            return StreamResponse(stream, (HttpStatusCode)status);
        };
    }
}
