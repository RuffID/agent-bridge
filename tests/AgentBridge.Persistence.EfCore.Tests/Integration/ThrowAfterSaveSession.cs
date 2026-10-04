using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <inheritdoc/>
/// <remarks>Тестовый отказ после настоящего SQL SaveChanges, до commit; транзакции и tracker не подменяются.</remarks>
public class ThrowAfterSaveSession(IUnitOfWorkContext<AgentBridgeContextKey> context) : IUnitOfWorkSession
{
    private readonly EfUnitOfWorkSession inner = new(context);
    /// <summary>Подтверждает, что SQL-сохранение действительно произошло до отказа.</summary>
    public int SavedEntries { get; private set; }
    /// <inheritdoc/>
    public Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken) => inner.BeginAsync(cancellationToken);
    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SavedEntries = await inner.SaveChangesAsync(cancellationToken);
        throw new IOException("Тестовый отказ после реального SaveChanges до commit.");
    }
    /// <inheritdoc/>
    public void Clear() => inner.Clear();
}
