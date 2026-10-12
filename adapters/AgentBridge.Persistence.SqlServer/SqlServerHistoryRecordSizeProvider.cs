using System.Linq.Expressions;
using System.Reflection;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using Microsoft.EntityFrameworkCore;

namespace AgentBridge.Persistence.SqlServer;

/// <summary>Полный server size expression для десяти mapped таблиц, включая owned reports и binary owner.</summary>
/// <remarks>Accounting = 256 + 2 × сумма DATALENGTH всех текстовых/binary-owner колонок.
/// DATALENGTH сохраняет trailing spaces и Unicode; коэффициент 2 ограничивает UTF-8 для UTF-16 storage.
/// Это консервативная граница payload, не физический размер БД или точный managed heap.</remarks>
public class SqlServerHistoryRecordSizeProvider : IHistoryRecordSizeProvider
{
    private const long FIXED_RECORD_BYTES = 256;
    private static readonly HashSet<Type> RECORD_TYPES =
    [
        typeof(DialogRecord), typeof(DialogTurnRecord), typeof(CanonicalItemRecord), typeof(ModelStepRecord),
        typeof(DialogContextRecord), typeof(DialogSettingsRecord), typeof(DialogCatalogRecord),
        typeof(DialogCatalogClockRecord), typeof(DialogCatalogChangeRecord), typeof(DialogRecoveryOperationRecord)
    ];
    private static readonly Expression<Func<string, long>> TEXT_SIZE =
        value => 2L * (long)(EF.Functions.DataLength(value) ?? 0);

    /// <inheritdoc/>
    public Expression<Func<TEntity, long>> Size<TEntity>() where TEntity : class
    {
        if (!RECORD_TYPES.Contains(typeof(TEntity)))
        {
            throw new NotSupportedException("history_record_type_unsupported");
        }

        ParameterExpression row = Expression.Parameter(typeof(TEntity), "row");
        Expression size = Expression.Add(Expression.Constant(FIXED_RECORD_BYTES), VariableBytes(row));
        return Expression.Lambda<Func<TEntity, long>>(size, row);
    }

    /// <summary>Каждая variable колонка учитывается; новый неизвестный reference type требует явной поддержки.</summary>
    private static Expression VariableBytes(Expression row)
    {
        Expression sum = Expression.Constant(0L);
        foreach (PropertyInfo property in row.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            MemberExpression value = Expression.Property(row, property);
            Expression? bytes = null;
            if (property.PropertyType == typeof(string))
            {
                bytes = new ReplaceText(value).Visit(TEXT_SIZE.Body);
            }
            else if (property.PropertyType == typeof(ModelResponseRecord))
            {
                bytes = VariableBytes(value);
            }
            else if (!property.PropertyType.IsValueType)
            {
                throw new NotSupportedException("history_record_column_unsupported");
            }

            if (bytes is not null)
            {
                sum = Expression.Add(sum, bytes);
            }
        }

        return sum;
    }

    /// <summary>Подставляет mapped member без Invoke/client evaluation.</summary>
    private class ReplaceText(Expression value) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => value;
    }
}
