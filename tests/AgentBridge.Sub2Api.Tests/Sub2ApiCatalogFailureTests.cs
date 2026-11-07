using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using HttpClientLibrary.Exceptions;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiCatalogFixture;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Отмена, deadline и cleanup проходят actual HttpClientLibrary без подавления первичной причины.</summary>
public class Sub2ApiCatalogFailureTests
{
    /// <summary>Invalid UTF-8 ID и оборванный JSON отклоняются, ресурсы освобождаются.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InvalidUtf8OrJsonIsRejected(bool invalidUtf8, bool unknownField)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        string json = "{\"object\":\"list\",\"data\":[{\"object\":\"model\",\"id\":\"X\"}]}";
        if (unknownField)
        {
            json = "{\"object\":\"list\",\"data\":[{\"object\":\"model\",\"id\":\"valid\",\"unknown\":\"X\"}]}";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (invalidUtf8)
        {
            bytes[Array.IndexOf(bytes, (byte)'X')] = 0xFF;
        }
        else
        {
            bytes = bytes[..^1];
        }

        ObservedStream stream = new(bytes);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelCatalogSnapshot> result = await fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        Sub2ApiCatalogTests.AssertSafe(string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Caller cancellation до операции не вызывает source или handler.</summary>
    [Fact]
    public async Task PreCanceledSettingsDoesNoIo()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        using CancellationTokenSource caller = new();
        caller.Cancel();

        // Act
        Exception? observed = await Record.ExceptionAsync(() => fixture.Settings.ReadAsync(fixture.Owner, ct: caller.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(observed);
        Assert.Equal(0, fixture.KeySource.Calls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Отмена проверяется на нужной стадии: до headers и при чтении success/error body.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(401)]
    public async Task CallerCancellationAtStagePreservesTokenAndReleasesResources(int status)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        using CancellationTokenSource caller = new();
        GatedStream stream = new();
        TaskCompletionSource enteredSend = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (_, ct) =>
        {
            if (status == 0)
            {
                enteredSend.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            return StreamResponse(stream, (HttpStatusCode)status);
        };
        Task entered = enteredSend.Task;
        if (status != 0)
        {
            entered = stream.Entered.Task;
        }

        // Act
        Task<ServiceResult<ModelCatalogSnapshot>> pending = fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), caller.Token);
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
    }

    /// <summary>Конечный deadline охватывает headers и оба body; DI подготовлен до начала отсчёта.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(503)]
    public async Task DeadlineAtStageIsExplicitTimeout(int status)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(timeout: TimeSpan.FromSeconds(2));
        GatedStream stream = new();
        TaskCompletionSource enteredSend = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (_, ct) =>
        {
            if (status == 0)
            {
                enteredSend.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            return StreamResponse(stream, (HttpStatusCode)status);
        };
        IModelCatalog catalog = fixture.Catalog;
        Task entered = enteredSend.Task;
        if (status != 0)
        {
            entered = stream.Entered.Task;
        }

        // Act
        Task<ServiceResult<ModelCatalogSnapshot>> pending = catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        ServiceResult<ModelCatalogSnapshot> result = await pending.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(entered.IsCompletedSuccessfully);
        Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(status != 0, stream.Disposed);
    }

    /// <summary>Malformed JSON плюс dispose failure не превращаются в обычный безопасный Rejected с потерей причины.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupFailureRemainsObservable(bool validJson)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        string json = "{malformed";
        if (validJson)
        {
            json = CATALOG;
        }

        IOException cleanup = new("private-cleanup-failure");
        ObservedStream stream = new(Encoding.UTF8.GetBytes(json)) { Cleanup = cleanup };
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(failure);
        if (validJson)
        {
            Assert.Same(cleanup, failure);
        }
        else
        {
            Assert.IsType<JsonException>(failure);
            Assert.Contains(cleanup, Assert.IsAssignableFrom<IReadOnlyList<Exception>>(failure.Data["HttpClientLibrary.CleanupExceptions"]));
        }

        Assert.True(stream.Disposed);
        Sub2ApiCatalogTests.AssertSafe(string.Join("", fixture.Logs.Entries));
        Assert.DoesNotContain("private-cleanup-failure", string.Join("", fixture.Logs.Entries));
    }

    /// <summary>HTTP error с cleanup failure сохраняет status и отдельную причину в actual library exception.</summary>
    [Fact]
    public async Task ErrorCleanupFailureIsNotSwallowed()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        IOException cleanup = new("private-error-cleanup");
        ObservedStream stream = new(Encoding.UTF8.GetBytes("{}")) { Cleanup = cleanup };
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream, HttpStatusCode.Unauthorized));

        // Act
        HttpRequestFailedException failure = await Assert.ThrowsAsync<HttpRequestFailedException>(() => fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        Assert.Contains(cleanup, failure.ProcessingFailure!.CleanupExceptions);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.DoesNotContain("private-error-cleanup", string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Неожиданный I/O сохраняет identity и не маскируется timeout или retry.</summary>
    [Fact]
    public async Task UnexpectedIoPropagates()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        IOException primary = new("private-transport-failure");
        fixture.Handler.Respond = (_, _) => throw primary;

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(primary, failure);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.DoesNotContain("private-transport-failure", string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Создаёт локальный response; actual pipeline владеет content и stream.</summary>
    private static HttpResponseMessage StreamResponse(Stream stream, HttpStatusCode status = HttpStatusCode.OK)
    {
        HttpResponseMessage response = new(status) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new("application/json");
        return response;
    }

    /// <summary>Наблюдает реальную очистку и её отказ, не создавая внешних ресурсов.</summary>
    private class ObservedStream(byte[] data) : MemoryStream(data)
    {
        internal bool Disposed { get; private set; }
        internal IOException? Cleanup { get; init; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
            if (Cleanup is not null)
            {
                throw Cleanup;
            }
        }
    }

    /// <summary>Стадия чтения подтверждается gate до отмены; бесконечное ожидание всегда связано с токеном.</summary>
    private class GatedStream : MemoryStream
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Disposed { get; private set; }

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
