using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <inheritdoc/>
internal class FakeUnitOfWorkSession : IUnitOfWorkSession
{
    public List<string> Events { get; } = [];
    public FakeWriteTransaction Transaction { get; }
    public Action? OnBegin { get; set; }
    public Func<Task>? OnSave { get; set; }
    public Action? OnClear { get; set; }
    public Exception? BeginError { get; set; }
    public Exception? ClearError { get; set; }
    /// <summary>Создаёт только управляемые границы; никакого EF provider execution.</summary>
    public FakeUnitOfWorkSession() => Transaction = new FakeWriteTransaction(Events);
    /// <inheritdoc/>
    public Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        Events.Add("begin");
        cancellationToken.ThrowIfCancellationRequested();
        if (BeginError is not null) { throw BeginError; }
        OnBegin?.Invoke();
        return Task.FromResult<IDbContextTransaction>(Transaction);
    }
    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        Events.Add("save");
        cancellationToken.ThrowIfCancellationRequested();
        if (OnSave is not null) { await OnSave(); }
        return 1;
    }
    /// <inheritdoc/>
    public void Clear()
    {
        Events.Add("clear");
        OnClear?.Invoke();
        if (ClearError is not null) { throw ClearError; }
    }
}
