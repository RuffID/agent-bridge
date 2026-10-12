using System.Linq.Expressions;
using AgentBridge.Application.Results;
using Microsoft.EntityFrameworkCore;

namespace AgentBridge.Persistence.EfCore.Reading;

/// <summary>Ограничивает server materialization через scalar preflight, byte predicate и Take в payload query.</summary>
/// <remarks>Используется только с Query базового EFCoreLibrary repository, внутри общего gate.
/// Проверка root/CAS принадлежит сценарию; изменение между queries не снимает I/O bounds.
/// Никаких retry, refill, обрезанных snapshots или client-side size predicates.</remarks>
public class HistoryQueryGuard(HistoryLoadBudget budget, IHistoryRecordSizeProvider sizes)
{
    /// <summary>Читает полный исходный query либо явно отказывает до загрузки oversized payload.</summary>
    public async Task<List<TEntity>> ReadAsync<TEntity>(IQueryable<TEntity> query, CancellationToken ct,
        int? take = null) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ct.ThrowIfCancellationRequested();
        int remaining = budget.Query();
        int limit = take is { } requested ? Math.Min(requested, remaining) : remaining;
        if (take is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        Expression<Func<TEntity, long>> size = sizes.Size<TEntity>();
        IQueryable<TEntity> expectedQuery = take is { } page ? query.Take(page) : query;
        List<long> expected = await expectedQuery.Select(size).Take(limit + 1).ToListAsync(ct);
        if (expected.Count > limit || expected.Any(bytes => bytes < 0 || bytes > budget.Limits.MaxRecordBytes))
        {
            throw new DialogHistoryLoadLimitException();
        }

        // Predicate действует в том же server query, что materialization. Preflight не является race guarantee.
        Expression<Func<TEntity, bool>> fits = Expression.Lambda<Func<TEntity, bool>>(
            Expression.LessThanOrEqual(size.Body, Expression.Constant(budget.Limits.MaxRecordBytes)), size.Parameters);
        List<TEntity> rows = await query.Where(fits).Take(limit).ToListAsync(ct);
        budget.Accept(rows.Count);
        if (rows.Count != expected.Count)
        {
            throw new DialogHistoryLoadConflictException();
        }

        return rows;
    }
}
