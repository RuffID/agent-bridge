using System.Text.Json;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.Tests.Integration.Sub2Api;

/// <inheritdoc/>
/// <remarks>Все SQL и commit настоящие; единственный отказ добавляется после выбранного committed journal state.</remarks>
internal class Sub2ApiCommitAcknowledgementSession(IUnitOfWorkContext<AgentBridgeContextKey> context,
    AgentBridgeDbContext database, Sub2ApiPersistenceFixture fixture) : IUnitOfWorkSession
{
    private readonly EfUnitOfWorkSession inner = new(context);
    private bool matches;

    /// <inheritdoc/>
    public async Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        matches = false;
        IDbContextTransaction transaction = await inner.BeginAsync(cancellationToken);
        return new Transaction(transaction, this);
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (EntityEntry<ModelStepRecord> entry in database.ChangeTracker.Entries<ModelStepRecord>())
        {
            if (entry.Entity.ToolAttemptsJson is null)
            {
                continue;
            }

            using JsonDocument journal = JsonDocument.Parse(entry.Entity.ToolAttemptsJson);
            foreach (JsonElement attempt in journal.RootElement.GetProperty("attempts").EnumerateArray())
            {
                if (attempt.GetProperty("state").GetInt32() == (int)fixture.FailCommitWhen!.Value)
                {
                    matches = true;
                }
            }
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        inner.Clear();
    }

    /// <summary>Проверяет отказ только после успешного настоящего commit.</summary>
    private void AfterCommit()
    {
        if (matches && fixture.CommitFailures == 0)
        {
            fixture.CommitFailures++;
            throw fixture.CommitFailure;
        }
    }

    /// <inheritdoc/>
    private class Transaction(IDbContextTransaction inner, Sub2ApiCommitAcknowledgementSession session) : IDbContextTransaction
    {
        public Guid TransactionId => inner.TransactionId;

        /// <inheritdoc/>
        public void Commit()
        {
            throw new InvalidOperationException("The persistence scenario requires async commit.");
        }

        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await inner.CommitAsync(cancellationToken);
            session.AfterCommit();
        }

        /// <inheritdoc/>
        public void Rollback()
        {
            inner.Rollback();
        }

        /// <inheritdoc/>
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return inner.RollbackAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            inner.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return inner.DisposeAsync();
        }
    }
}
