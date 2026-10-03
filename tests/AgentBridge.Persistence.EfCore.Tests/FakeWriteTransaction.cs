using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <inheritdoc/>
internal class FakeWriteTransaction(List<string> events) : IDbContextTransaction
{
    public Action? OnCommit { get; set; }
    public Action? OnRollback { get; set; }
    public Exception? CommitError { get; set; }
    public Exception? RollbackError { get; set; }
    public Exception? DisposeError { get; set; }
    /// <inheritdoc/>
    public Guid TransactionId { get; } = Guid.NewGuid();
    /// <inheritdoc/>
    public void Commit() => throw new NotSupportedException();
    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        events.Add("commit");
        cancellationToken.ThrowIfCancellationRequested();
        OnCommit?.Invoke();
        if (CommitError is not null) { throw CommitError; }
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    public void Rollback() => throw new NotSupportedException();
    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        events.Add("rollback");
        cancellationToken.ThrowIfCancellationRequested();
        OnRollback?.Invoke();
        if (RollbackError is not null) { throw RollbackError; }
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    public void Dispose() => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        events.Add("dispose");
        if (DisposeError is not null) { throw DisposeError; }
        return ValueTask.CompletedTask;
    }
}
