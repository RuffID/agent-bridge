using System.Linq.Expressions;

namespace AgentBridge.Persistence.EfCore.Reading;

/// <summary>Provider-owned server expression для полной строки; отсутствие поддержки отклоняется до payload I/O.</summary>
public interface IHistoryRecordSizeProvider
{
    /// <summary>Возвращает консервативный размер всех variable columns, reports и provenance, плюс fixed allowance.</summary>
    Expression<Func<TEntity, long>> Size<TEntity>() where TEntity : class;
}
