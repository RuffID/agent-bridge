using AgentBridge.Persistence.EfCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Единый маппинг полного отчёта внутри строки model step или compact без отдельного provider repository.</summary>
internal static class ModelResponseMapping
{
    /// <summary>Сохраняет полный JSON как text без provider-нормализации; колонки отчёта не входят в input-items.</summary>
    public static void Configure(ComplexPropertyBuilder<ModelResponseRecord> builder)
    {
        builder.IsRequired();
        builder.Property(record => record.FormatVersion).HasColumnName("ResponseFormatVersion").IsRequired();
        builder.Property(record => record.Status).HasColumnName("ResponseStatus").HasConversion<int>().IsRequired();
        builder.Property(record => record.OutputJson).HasColumnName("ResponseOutputJson").HasColumnType("text").IsRequired();
        builder.Property(record => record.EnvelopeJson).HasColumnName("ResponseEnvelopeJson").HasColumnType("text");
        builder.Property(record => record.ContinuationJson).HasColumnName("ResponseContinuationJson").HasColumnType("text");
        builder.Property(record => record.ErrorType).HasColumnName("ResponseErrorType").HasConversion<int>();
        builder.Property(record => record.ErrorMessage).HasColumnName("ResponseErrorMessage").HasColumnType("text");
    }

    /// <summary>Ограничения локальной формы lifecycle; terminal prefix и atomic guards выполняет будущий UoW.</summary>
    public static void ConfigureChecks<TEntity>(TableBuilder<TEntity> table) where TEntity : class
    {
        string prefix = typeof(TEntity).Name;
        table.HasCheckConstraint($"CK_{prefix}_ResponseVersion", "\"ResponseFormatVersion\" = 1");
        table.HasCheckConstraint($"CK_{prefix}_ResponseStatus", "\"ResponseStatus\" BETWEEN 0 AND 3");
        table.HasCheckConstraint($"CK_{prefix}_ResponseError",
            "(\"ResponseStatus\" = 2 AND \"ResponseErrorType\" IS NOT NULL AND \"ResponseErrorType\" BETWEEN 0 AND 8 AND \"ResponseErrorMessage\" IS NOT NULL AND length(\"ResponseErrorMessage\") > 0) OR " +
            "(\"ResponseStatus\" <> 2 AND \"ResponseErrorType\" IS NULL AND \"ResponseErrorMessage\" IS NULL)");
    }
}
