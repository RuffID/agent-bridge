using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Base Query double: применяет server expression до выдачи rows и фиксирует materialization bounds, без БД.</summary>
internal class FakeHistoryQueryRepository<TEntity>(List<TEntity> records) : IContextQueryRepository<TEntity, AgentBridgeContextKey>
    where TEntity : class
{
    private readonly List<TEntity> _records = records;
    public List<Expression> ScalarQueries { get; } = [];
    public List<Expression> PayloadQueries { get; } = [];
    public int PayloadRows { get; private set; }
    public List<int> ScalarRows { get; } = [];
    public Action<bool>? BeforeExecute { get; set; }
    public Exception? Error { get; set; }

    /// <inheritdoc/>
    public IQueryable<TEntity> Query(bool asNoTracking = false) => new Queryable<TEntity>(_records.AsQueryable().Expression, this);

    /// <summary>Измеряет byte-accounting через тот же SQL expression с изолированным DATALENGTH evaluator.</summary>
    internal static long Size(TEntity row)
    {
        Expression<Func<TEntity, long>> expression = new Persistence.SqlServer.SqlServerHistoryRecordSizeProvider().Size<TEntity>();
        return Expression.Lambda<Func<TEntity, long>>(new DataLengthVisitor().Visit(expression.Body), expression.Parameters).Compile()(row);
    }

    /// <summary>Создаёт deferred LINQ query с наблюдаемой async materialization.</summary>
    private class Queryable<T>(Expression expression, FakeHistoryQueryRepository<TEntity> owner) :
        IOrderedQueryable<T>, IAsyncEnumerable<T>
    {
        public Type ElementType => typeof(T);
        public Expression Expression => expression;
        public IQueryProvider Provider => new Provider(owner);
        public IEnumerator<T> GetEnumerator() => Execute(CancellationToken.None).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new Enumerator<T>(Execute(cancellationToken).GetEnumerator(), cancellationToken);

        private IEnumerable<T> Execute(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            bool scalar = typeof(T) == typeof(long);
            owner.BeforeExecute?.Invoke(scalar);
            if (owner.Error is not null)
            {
                throw owner.Error;
            }

            (scalar ? owner.ScalarQueries : owner.PayloadQueries).Add(expression);
            Expression evaluated = new DataLengthVisitor().Visit(expression);
            List<T> rows = owner._records.AsQueryable().Provider.CreateQuery<T>(evaluated).ToList();
            if (scalar)
            {
                owner.ScalarRows.Add(rows.Count);
            }
            else
            {
                owner.PayloadRows += rows.Count;
            }

            return rows;
        }
    }

    /// <summary>Сохраняет исходный expression; не имитирует SQL/provider concurrency.</summary>
    private class Provider(FakeHistoryQueryRepository<TEntity> owner) : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new Queryable<TElement>(expression, owner);
        public object? Execute(Expression expression) => throw new NotSupportedException();
        public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();
    }

    /// <summary>Ожидает каждое перечисление и сохраняет caller cancellation.</summary>
    private class Enumerator<T>(IEnumerator<T> inner, CancellationToken ct) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(inner.MoveNext());
        }
    }

    /// <summary>Только I/O double вычисляет SQL DATALENGTH как UTF-16 storage bytes; production expression неизменён.</summary>
    private class DataLengthVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(SqlServerDbFunctionsExtensions) && node.Method.Name == "DataLength")
            {
                return Expression.Call(typeof(FakeHistoryQueryRepository<TEntity>), nameof(StorageLength), [], Visit(node.Arguments[1]));
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>Учитывает NUL, trailing spaces и непарные UTF-16 code units без нормализации.</summary>
    public static int? StorageLength(string? value) => value is null ? null : checked(value.Length * 2);
}
