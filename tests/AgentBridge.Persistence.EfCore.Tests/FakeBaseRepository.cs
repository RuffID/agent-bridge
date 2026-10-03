using System.Linq.Expressions;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Изолированная заглушка точных base API: чтение LINQ в памяти, staging отдельно от исходных строк.</summary>
internal class FakeBaseRepository<TEntity> :
    IContextGetItemByPredicateRepository<TEntity, AgentBridgeContextKey>,
    IContextCreateItemRepository<TEntity, AgentBridgeContextKey>,
    IContextUpdateItemRepository<TEntity, AgentBridgeContextKey>,
    IContextDeleteItemRepository<TEntity, AgentBridgeContextKey> where TEntity : class
{
    public List<TEntity> Records { get; } = [];
    public List<TEntity> Created { get; } = [];
    public List<TEntity> Updated { get; } = [];
    public List<TEntity> Deleted { get; } = [];
    public IEnumerable<TEntity>? CreatedRange { get; private set; }
    public IEnumerable<TEntity>? UpdatedRange { get; private set; }
    public IEnumerable<TEntity>? DeletedRange { get; private set; }
    public int ReadCalls { get; private set; }
    public bool? LastAsNoTracking { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public int? LastTake { get; private set; }
    public Exception? ReadException { get; set; }
    public Action? BeforeRead { get; set; }

    /// <summary>Очищает изолированный staged пакет после fake transaction; исходные строки не меняет.</summary>
    public void ClearStaging()
    {
        Created.Clear();
        Updated.Clear();
        Deleted.Clear();
        CreatedRange = null;
        UpdatedRange = null;
        DeletedRange = null;
    }

    /// <inheritdoc/>
    public Task<TEntity?> GetItemByPredicateAsync(Expression<Func<TEntity, bool>> predicate, bool asNoTracking = false,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default)
    {
        IQueryable<TEntity> query = Read(asNoTracking, ct);
        if (include is not null)
        {
            query = include(query);
        }
        return Task.FromResult(query.FirstOrDefault(predicate));
    }

    /// <inheritdoc/>
    public Task<List<TEntity>> GetItemsByPredicateAsync(Expression<Func<TEntity, bool>>? predicate = null,
        int skip = 0, int? take = null, bool asNoTracking = false,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default)
    {
        IQueryable<TEntity> query = Read(asNoTracking, ct);
        LastTake = take;
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }
        if (include is not null)
        {
            query = include(query);
        }
        if (skip > 0)
        {
            query = query.Skip(skip);
        }
        if (take is > 0)
        {
            query = query.Take(take.Value);
        }
        return Task.FromResult(query.ToList());
    }

    /// <inheritdoc/>
    public void Create(TEntity item) => Created.Add(item);
    /// <inheritdoc/>
    public void Update(TEntity item) => Updated.Add(item);
    /// <inheritdoc/>
    public void Delete(TEntity item) => Deleted.Add(item);
    /// <inheritdoc/>
    public void CreateRange(IEnumerable<TEntity> entities) => CreatedRange = entities;
    /// <inheritdoc/>
    public void UpdateRange(IEnumerable<TEntity> entities) => UpdatedRange = entities;
    /// <inheritdoc/>
    public void DeleteRange(IEnumerable<TEntity> entities) => DeletedRange = entities;

    /// <summary>Фиксирует параметры base read и не имитирует асинхронный EF provider.</summary>
    private IQueryable<TEntity> Read(bool asNoTracking, CancellationToken cancellationToken)
    {
        ReadCalls++;
        LastAsNoTracking = asNoTracking;
        LastCancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (ReadException is not null)
        {
            throw ReadException;
        }
        BeforeRead?.Invoke();
        return Records.AsQueryable();
    }
}
