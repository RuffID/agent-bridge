using AgentBridge.Persistence.EfCore.Mapping;
using AgentBridge.Persistence.EfCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentBridge.Persistence.EfCore;

/// <summary>Общий контекст persistence DTO; операции выполняются через EFCoreLibrary и сценарные UoW.</summary>
public class AgentBridgeDbContext : DbContext
{
    /// <summary>Получает options выбранного приложением провайдера; не открывает соединение.</summary>
    public AgentBridgeDbContext(DbContextOptions<AgentBridgeDbContext> options) : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        string ownerCollation = Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" => "BINARY",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
            _ => throw new InvalidOperationException("Контекст AgentBridge требует явного SQLite/PostgreSQL.")
        };
        ConfigureDialog(modelBuilder, ownerCollation);
        ConfigureTurn(modelBuilder);
        ConfigureItem(modelBuilder);
        ConfigureModelStep(modelBuilder);
        ConfigureContext(modelBuilder);
    }

    /// <summary>Фиксированные метаданные и optimistic token; guards existence/expiry/ownership здесь не исполняются.</summary>
    private static void ConfigureDialog(ModelBuilder modelBuilder, string ownerCollation)
    {
        EntityTypeBuilder<DialogRecord> builder = modelBuilder.Entity<DialogRecord>();
        builder.ToTable("Dialogs", table =>
        {
            table.HasCheckConstraint("CK_Dialog_Revision", "\"Revision\" >= 0");
            table.HasCheckConstraint("CK_Dialog_ContentBytes", "\"ContentBytes\" >= 0");
            table.HasCheckConstraint("CK_Dialog_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
            table.HasCheckConstraint("CK_Dialog_LastChanged", "\"LastChangedAtUtc\" >= \"CreatedAtUtc\"");
            table.HasCheckConstraint("CK_Dialog_Owner", "length(\"OwnerId\") > 0");
            table.HasCheckConstraint("CK_Dialog_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000' AND \"IncarnationId\" <> '00000000-0000-0000-0000-000000000000'");
        });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.IncarnationId).ValueGeneratedNever().IsConcurrencyToken()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.Revision).IsConcurrencyToken();
        builder.Property(record => record.OwnerId).IsRequired().UseCollation(ownerCollation).IsConcurrencyToken()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.CreatedAtUtc).HasConversion<UtcTicksConverter>()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.ExpiresAtUtc).HasConversion<UtcTicksConverter>().IsConcurrencyToken()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.LastChangedAtUtc).HasConversion<UtcTicksConverter>();
        builder.HasIndex(record => new { record.ExpiresAtUtc, record.Id });
    }

    /// <summary>Уникальность turn ID и порядка внутри диалога; состояние конечного времени согласовано со статусом.</summary>
    private static void ConfigureTurn(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<DialogTurnRecord> builder = modelBuilder.Entity<DialogTurnRecord>();
        builder.ToTable("DialogTurns", table =>
        {
            table.HasCheckConstraint("CK_Turn_Sequence", "\"Sequence\" > 0");
            table.HasCheckConstraint("CK_Turn_Status", "\"Status\" BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_Turn_Finished", "(\"Status\" = 0 AND \"FinishedAtUtc\" IS NULL) OR (\"Status\" <> 0 AND \"FinishedAtUtc\" IS NOT NULL AND \"FinishedAtUtc\" >= \"StartedAtUtc\")");
        });
        builder.HasKey(record => new { record.DialogId, record.Id });
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.Status).HasConversion<int>();
        builder.Property(record => record.StartedAtUtc).HasConversion<UtcTicksConverter>();
        builder.Property(record => record.FinishedAtUtc).HasConversion<UtcTicksConverter>();
        builder.HasIndex(record => new { record.DialogId, record.Sequence }).IsUnique();
        builder.HasOne<DialogRecord>().WithMany().HasForeignKey(record => record.DialogId).OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Составная связь гарантирует принадлежность turn тому же диалогу; порядок задаёт ключ, не выдача БД.</summary>
    private static void ConfigureItem(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<CanonicalItemRecord> builder = modelBuilder.Entity<CanonicalItemRecord>();
        builder.ToTable("CanonicalItems", table => table.HasCheckConstraint("CK_Item_Sequence", "\"Sequence\" > 0"));
        builder.HasKey(record => new { record.DialogId, record.TurnId, record.Sequence });
        builder.Property(record => record.Sequence).ValueGeneratedNever();
        builder.Property(record => record.ContentJson).HasColumnType("text").IsRequired();
        builder.HasOne<DialogTurnRecord>().WithMany().HasForeignKey(record => new { record.DialogId, record.TurnId })
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Шаг имеет собственную идентичность и последовательность; полный отчёт сохраняется отдельными колонками.</summary>
    private static void ConfigureModelStep(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<ModelStepRecord> builder = modelBuilder.Entity<ModelStepRecord>();
        builder.ToTable("ModelSteps", table =>
        {
            table.HasCheckConstraint("CK_Step_Sequence", "\"Sequence\" > 0");
            ModelResponseMapping.ConfigureChecks(table);
        });
        builder.HasKey(record => new { record.DialogId, record.TurnId, record.Id });
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.HasIndex(record => new { record.DialogId, record.TurnId, record.Sequence }).IsUnique();
        builder.HasOne<DialogTurnRecord>().WithMany().HasForeignKey(record => new { record.DialogId, record.TurnId })
            .OnDelete(DeleteBehavior.Cascade);
        ModelResponseMapping.Configure(builder.ComplexProperty(record => record.Response));
    }

    /// <summary>Все принятые версии compact; максимальная Version определяет активное окно без удаления истории.</summary>
    private static void ConfigureContext(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<DialogContextRecord> builder = modelBuilder.Entity<DialogContextRecord>();
        builder.ToTable("DialogContexts", table =>
        {
            table.HasCheckConstraint("CK_Context_Version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_Context_Prefix", "\"ThroughTurnSequence\" >= 0");
            table.HasCheckConstraint("CK_Context_Completed", "\"ResponseStatus\" = 0");
            ModelResponseMapping.ConfigureChecks(table);
        });
        builder.HasKey(record => new { record.DialogId, record.Version });
        builder.Property(record => record.Version).ValueGeneratedNever();
        builder.Property(record => record.CreatedAtUtc).HasConversion<UtcTicksConverter>();
        builder.HasOne<DialogRecord>().WithMany().HasForeignKey(record => record.DialogId).OnDelete(DeleteBehavior.Cascade);
        ModelResponseMapping.Configure(builder.ComplexProperty(record => record.Compaction));
    }
}
