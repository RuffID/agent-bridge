using AgentBridge.Application.Ports;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Hosting.Tests;

/// <summary>Scoped app-owned source; поддерживает отдельно наблюдаемые sync и async disposal.</summary>
internal class HostingKeySource(HostingFixture fixture) : IIndividualModelKeySource, IDisposable, IAsyncDisposable
{
    internal int Calls;
    internal bool Disposed;
    internal bool AsyncDisposed;

    /// <inheritdoc/>
    public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult<string?>("synthetic-individual-key");
    }

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        fixture.KeyDisposeEntered.TrySetResult();
        if (fixture.HoldKeyDisposal) await fixture.KeyDisposeRelease.Task.WaitAsync(HostingFixture.BUDGET);
        Disposed = true;
        AsyncDisposed = true;
        if (fixture.KeyDisposalFailure is { } error) throw error;
    }
}
