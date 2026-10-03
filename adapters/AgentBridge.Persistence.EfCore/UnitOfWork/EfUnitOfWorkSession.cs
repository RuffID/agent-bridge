using System.Transactions;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class EfUnitOfWorkSession(IUnitOfWorkContext<AgentBridgeContextKey> context) : IUnitOfWorkSession
{
    /// <inheritdoc/>
    public Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        if (Transaction.Current is not null || context.Database.CurrentTransaction is not null ||
            context.Database.GetEnlistedTransaction() is not null || context.Database.CreateExecutionStrategy().RetriesOnFailure)
        {
            throw new InvalidOperationException("Сценарий требует собственной transaction без автоматических retry.");
        }
        return context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
    /// <inheritdoc/>
    public void Clear() => context.ClearChangeTracker();
}
