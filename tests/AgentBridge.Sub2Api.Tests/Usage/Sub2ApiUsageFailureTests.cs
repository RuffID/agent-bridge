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

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Отмена, deadline и очистка actual usage transport с локальными gated streams.</summary>
public class Sub2ApiUsageFailureTests
{
    /// <summary>Ранняя отмена и невалидный ключ не достигают отложенной factory.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LocalFailureDoesNotCreateHttp(bool canceled)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string key = "bad key";
        if (canceled)
        {
            key = INDIVIDUAL_KEY;
            caller.Cancel();
        }

        ServiceResult<ModelAuxiliaryResult>? result = null;

        // Act
        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            result = await Gateway(fixture).ReadUsageAsync(new(key), caller.Token);
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

    /// <summary>Caller cancellation на send/success body/error body сохраняет исходный токен и освобождает ресурсы.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(401)]
    public async Task CallerCancellationAtStage(int status)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Sub2ApiTestStream stream = new([], 1) { BlockAtEnd = true };
        TaskCompletionSource send = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfigureStage(fixture, stream, send, status);
        Task entered = stream.Blocked.Task;
        if (status == 0)
        {
            entered = send.Task;
        }

        // Act
        Task<ServiceResult<ModelAuxiliaryResult>> pending = Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY), caller.Token);
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
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>GenerationTimeout охватывает send и чтение обоих видов body, без повторного запроса.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(503)]
    public async Task DeadlineAtStage(int status)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(timeout: TimeSpan.FromSeconds(2), auxiliary: true);
        Sub2ApiTestStream stream = new([], 1) { BlockAtEnd = true };
        TaskCompletionSource send = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfigureStage(fixture, stream, send, status);
        IModelAuxiliaryGateway gateway = Gateway(fixture);
        Task entered = stream.Blocked.Task;
        if (status == 0)
        {
            entered = send.Task;
        }

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await gateway.ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(entered.IsCompletedSuccessfully);
        Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(status != 0, stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>При одновременно истёкшем deadline исходная отмена caller остаётся приоритетной.</summary>
    [Fact]
    public async Task CallerWinsWhenBothTokensAreCanceled()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(timeout: TimeSpan.FromSeconds(1), auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Handler.Respond = async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                caller.Cancel();
                throw;
            }

            throw new InvalidOperationException();
        };
        IModelAuxiliaryGateway gateway = Gateway(fixture);

        // Act
        OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.ReadUsageAsync(new(INDIVIDUAL_KEY), caller.Token));

        // Assert
        Assert.Equal(caller.Token, failure.CancellationToken);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Транспортный HTTP-отказ не раскрывает секретный текст исключения и не повторяется.</summary>
    [Fact]
    public async Task NetworkFailureIsSafe()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        fixture.Handler.Respond = (_, _) => throw new HttpRequestException(PRIVATE);

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Более короткий timeout borrowed HttpClient тоже возвращается безопасным Timeout.</summary>
    [Fact]
    public async Task HttpClientTimeoutIsSafe()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        fixture.Http.Timeout = TimeSpan.FromMilliseconds(250);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException();
        };
        IModelAuxiliaryGateway gateway = Gateway(fixture);

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await gateway.ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(entered.Task.IsCompletedSuccessfully);
        Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Отмена body с cleanup failure сохраняет обе причины, включая HTTP-статус error response.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledBodyCleanupRemainsObservable(bool httpError)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IOException cleanup = new(PRIVATE);
        Sub2ApiTestStream stream = new([], 1) { BlockAtEnd = true, CleanupFailure = cleanup };
        HttpStatusCode status = HttpStatusCode.OK;
        if (httpError)
        {
            status = HttpStatusCode.Unauthorized;
        }

        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream, status));

        // Act
        Task<ServiceResult<ModelAuxiliaryResult>> pending = Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY), caller.Token);
        Exception? failure = null;
        try
        {
            await stream.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            caller.Cancel();
            failure = await Record.ExceptionAsync(() => pending);
        }
        finally
        {
            caller.Cancel();
            await Record.ExceptionAsync(() => pending);
        }

        // Assert
        OperationCanceledException cancellation = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        if (httpError)
        {
            HttpResponseCanceledException canceled = Assert.IsType<HttpResponseCanceledException>(failure);
            Assert.Equal(HttpStatusCode.Unauthorized, canceled.StatusCode);
            Assert.Contains(cleanup, canceled.ProcessingFailure.CleanupExceptions);
        }
        else
        {
            Assert.Contains(cleanup, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(cancellation.Data["HttpClientLibrary.CleanupExceptions"]));
        }

        Assert.True(stream.Disposed);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Primary плюс cleanup failure не превращаются в безопасный Rejected с потерей причины.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupFailureRemainsObservable(bool httpError)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        IOException cleanup = new(PRIVATE);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes("{malformed"), 3) { CleanupFailure = cleanup };
        HttpStatusCode status = HttpStatusCode.OK;
        if (httpError)
        {
            status = HttpStatusCode.Forbidden;
        }

        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream, status));

        // Act
        Exception? failure = await Record.ExceptionAsync(() => Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken));

        // Assert
        if (httpError)
        {
            HttpRequestFailedException http = Assert.IsType<HttpRequestFailedException>(failure);
            Assert.Equal(HttpStatusCode.Forbidden, http.StatusCode);
            Assert.Contains(cleanup, http.ProcessingFailure!.CleanupExceptions);
        }
        else
        {
            JsonException json = Assert.IsType<JsonException>(failure);
            Assert.Contains(cleanup, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(json.Data["HttpClientLibrary.CleanupExceptions"]));
        }

        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Standalone cleanup failure после успешного JSON не теряется в результате.</summary>
    [Fact]
    public async Task SuccessfulBodyCleanupFailureIsObservable()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        IOException cleanup = new(PRIVATE);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes("{\"mode\":\"unrestricted\"}"), 3) { CleanupFailure = cleanup };
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        Exception? failure = await Record.ExceptionAsync(() => Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(cleanup, failure);
        Assert.True(stream.Disposed);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Задаёт gate стадии без HTTP, host или фоновых задач вне проверяемого вызова.</summary>
    private static void ConfigureStage(Sub2ApiCatalogFixture fixture, Sub2ApiTestStream stream,
        TaskCompletionSource send, int status)
    {
        fixture.Handler.Respond = async (_, ct) =>
        {
            if (status == 0)
            {
                send.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            return StreamResponse(stream, (HttpStatusCode)status);
        };
    }
}
