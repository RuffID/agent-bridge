using System.Net;
using System.Text;

namespace AgentBridge.Hosting.Tests;

/// <summary>Локальные send/body gates actual HTTP pipeline, без сети и задержек.</summary>
internal class HostingHandler : HttpMessageHandler
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly List<BodyStream> Bodies = [];
    internal bool BlockSend;
    internal bool BlockBody;
    internal bool Disposed;
    internal int Calls;
    internal Exception? ReadFailure;
    internal Exception? DisposeFailure;
    internal CancellationToken ObservedToken;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Disposed) throw new ObjectDisposedException(nameof(HostingHandler));
        Calls++;
        ObservedToken = cancellationToken;
        if (request.RequestUri?.AbsoluteUri != "https://example.invalid/prefix/v1/models")
            throw new InvalidOperationException("Непредвиденный запрос hosting-теста.");
        if (request.Headers.Authorization?.ToString() is not ("Bearer synthetic-individual-key" or "Bearer synthetic-shared-key"))
            throw new InvalidOperationException("Pipeline не передал app-owned key.");
        if (BlockSend) await WaitForCancellationAsync(cancellationToken);

        BodyStream stream = new(this);
        Bodies.Add(stream);
        return new(HttpStatusCode.OK) { Content = new StreamContent(stream) };
    }

    /// <summary>Отмечает реальную отмену transport, но удерживает возврат до явного gate приложения.</summary>
    private async Task WaitForCancellationAsync(CancellationToken ct)
    {
        Entered.TrySetResult();
        TaskCompletionSource never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try { await never.Task.WaitAsync(HostingFixture.BUDGET, ct); }
        catch (OperationCanceledException)
        {
            Canceled.TrySetResult();
            await Release.Task.WaitAsync(HostingFixture.BUDGET);
            throw;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing) Disposed = true;
        base.Dispose(disposing);
    }

    /// <summary>Непустой каталог не позволяет Content-Length=0 обойти actual чтение body.</summary>
    internal class BodyStream(HostingHandler handler) : MemoryStream(Encoding.UTF8.GetBytes("""{"object":"list","data":[{"id":"gpt-4.1","object":"model","created":1,"owned_by":"codex-lb","metadata":{"context_window":50000,"input_context_window":40000,"input_modalities":["text"],"supported_in_api":true,"supported_reasoning_levels":[{"effort":"medium"}]}}]}"""))
    {
        internal bool Disposed;
        private bool failureThrown;

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            handler.ObservedToken = cancellationToken;
            if (handler.BlockBody) await handler.WaitForCancellationAsync(cancellationToken);
            if (handler.ReadFailure is { } primary)
            {
                handler.Entered.TrySetResult();
                await handler.Release.Task.WaitAsync(HostingFixture.BUDGET, cancellationToken);
                throw primary;
            }

            return await base.ReadAsync(buffer, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
            if (disposing && !failureThrown && handler.DisposeFailure is { } error)
            {
                failureThrown = true;
                throw error;
            }
        }
    }
}
