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
        ProviderModelMapping mapping = new(Database.ProviderName!);
        ConfigureDialog(modelBuilder, mapping);
        ConfigureTurn(modelBuilder, mapping);
        ConfigureItem(modelBuilder, mapping);
        ConfigureModelStep(modelBuilder, mapping);
        ConfigureContext(modelBuilder, mapping);
        ConfigureSettings(modelBuilder, mapping);
        CatalogModelMapping.Configure(modelBuilder, mapping);
    }

    /// <summary>Фиксированные метаданные и optimistic token; guards existence/expiry/ownership здесь не исполняются.</summary>
    private static void ConfigureDialog(ModelBuilder modelBuilder, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<DialogRecord> builder = modelBuilder.Entity<DialogRecord>();
        builder.ToTable("Dialogs", table =>
        {
            table.HasCheckConstraint("CK_Dialog_Revision", mapping.Check("\"Revision\" >= 0"));
            table.HasCheckConstraint("CK_Dialog_ContentBytes", mapping.Check("\"ContentBytes\" >= 0"));
            table.HasCheckConstraint("CK_Dialog_Expiry", mapping.Check("\"ExpiresAtUtc\" > \"CreatedAtUtc\""));
            table.HasCheckConstraint("CK_Dialog_LastChanged", mapping.Check("\"LastChangedAtUtc\" >= \"CreatedAtUtc\""));
            table.HasCheckConstraint("CK_Dialog_Owner", mapping.Check("length(\"OwnerId\") > 0"));
            table.HasCheckConstraint("CK_Dialog_Identity", mapping.Check("\"Id\" <> '00000000-0000-0000-0000-000000000000' AND \"IncarnationId\" <> '00000000-0000-0000-0000-000000000000'"));
        });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.IncarnationId).ValueGeneratedNever().IsConcurrencyToken()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.Revision).IsConcurrencyToken();
        builder.Property(record => record.CatalogRegistered).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.RuntimeJson).HasColumnType(mapping.TextType).IsConcurrencyToken();
        PropertyBuilder<string> owner = builder.Property(record => record.OwnerId).IsRequired().IsConcurrencyToken();
        if (mapping.IsSqlServer)
        {
            owner.HasConversion<OrdinalOwnerConverter>().HasColumnType("varbinary(max)");
        }
        else
        {
            owner.UseCollation(mapping.OwnerCollation);
        }
        owner
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.CreatedAtUtc).HasConversion<UtcTicksConverter>()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.ExpiresAtUtc).HasConversion<UtcTicksConverter>().IsConcurrencyToken()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(record => record.LastChangedAtUtc).HasConversion<UtcTicksConverter>();
        builder.HasIndex(record => new { record.ExpiresAtUtc, record.Id });
    }

    /// <summary>Уникальность turn ID и порядка внутри диалога; состояние конечного времени согласовано со статусом.</summary>
    private static void ConfigureTurn(ModelBuilder modelBuilder, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<DialogTurnRecord> builder = modelBuilder.Entity<DialogTurnRecord>();
        builder.ToTable("DialogTurns", table =>
        {
            table.HasCheckConstraint("CK_Turn_Sequence", mapping.Check("\"Sequence\" > 0"));
            table.HasCheckConstraint("CK_Turn_Status", mapping.Check("\"Status\" BETWEEN 0 AND 4"));
            table.HasCheckConstraint("CK_Turn_Finished", mapping.Check("(\"Status\" = 0 AND \"FinishedAtUtc\" IS NULL) OR (\"Status\" <> 0 AND \"FinishedAtUtc\" IS NOT NULL AND \"FinishedAtUtc\" >= \"StartedAtUtc\")"));
        });
        builder.HasKey(record => new { record.DialogId, record.Id });
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.Status).HasConversion<int>();
        builder.Property(record => record.SettingsJson).HasColumnType(mapping.TextType);
        builder.Property(record => record.StartedAtUtc).HasConversion<UtcTicksConverter>();
        builder.Property(record => record.FinishedAtUtc).HasConversion<UtcTicksConverter>();
        builder.HasIndex(record => new { record.DialogId, record.Sequence }).IsUnique();
        builder.HasOne<DialogRecord>().WithMany().HasForeignKey(record => record.DialogId).OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Составная связь гарантирует принадлежность turn тому же диалогу; порядок задаёт ключ, не выдача БД.</summary>
    private static void ConfigureItem(ModelBuilder modelBuilder, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<CanonicalItemRecord> builder = modelBuilder.Entity<CanonicalItemRecord>();
        builder.ToTable("CanonicalItems", table => table.HasCheckConstraint("CK_Item_Sequence", mapping.Check("\"Sequence\" > 0")));
        builder.HasKey(record => new { record.DialogId, record.TurnId, record.Sequence });
        builder.Property(record => record.Sequence).ValueGeneratedNever();
        builder.Property(record => record.ContentJson).HasColumnType(mapping.TextType).IsRequired();
        builder.Property(record => record.SavedAtUtc).HasConversion<UtcTicksConverter>();
        builder.HasOne<DialogTurnRecord>().WithMany().HasForeignKey(record => new { record.DialogId, record.TurnId })
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Шаг имеет собственную идентичность и последовательность; полный отчёт сохраняется отдельными колонками.</summary>
    private static void ConfigureModelStep(ModelBuilder modelBuilder, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<ModelStepRecord> builder = modelBuilder.Entity<ModelStepRecord>();
        builder.ToTable("ModelSteps", table =>
        {
            table.HasCheckConstraint("CK_Step_Sequence", mapping.Check("\"Sequence\" > 0"));
            ModelResponseMapping.ConfigureChecks(table, mapping);
        });
        builder.HasKey(record => new { record.DialogId, record.TurnId, record.Id });
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.HasIndex(record => new { record.DialogId, record.TurnId, record.Sequence }).IsUnique();
        builder.Property(record => record.ToolAttemptsJson).HasColumnType(mapping.TextType);
        builder.HasOne<DialogTurnRecord>().WithMany().HasForeignKey(record => new { record.DialogId, record.TurnId })
            .OnDelete(DeleteBehavior.Cascade);
        ModelResponseMapping.Configure(builder.ComplexProperty(record => record.Response), mapping);
    }

    /// <summary>Все принятые версии compact; максимальная Version определяет активное окно без удаления истории.</summary>
    private static void ConfigureContext(ModelBuilder modelBuilder, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<DialogContextRecord> builder = modelBuilder.Entity<DialogContextRecord>();
        builder.ToTable("DialogContexts", table =>
        {
            table.HasCheckConstraint("CK_Context_Version", mapping.Check("\"Version\" > 0"));
            table.HasCheckConstraint("CK_Context_Prefix", mapping.Check("\"ThroughTurnSequence\" >= 0"));
            table.HasCheckConstraint("CK_Context_Completed", mapping.Check("\"ResponseStatus\" = 0"));
            ModelResponseMapping.ConfigureChecks(table, mapping);
        });
        builder.HasKey(record => new { record.DialogId, record.Version });
        builder.Property(record => record.Version).ValueGeneratedNever();
        builder.Property(record => record.CreatedAtUtc).HasConversion<UtcTicksConverter>();
        builder.Property(record => record.RecoveryRevision);
        builder.HasOne<DialogRecord>().WithMany().HasForeignKey(record => record.DialogId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(record => record.SelectedModel).HasColumnType(mapping.TextType);
        ModelResponseMapping.Configure(builder.ComplexProperty(record => record.Compaction), mapping);
    }

    /// <summary>Independent CAS версия, cascade вместе с диалогом; root revision не меняется.</summary>
    private static void ConfigureSettings(ModelBuilder modelBuilder, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<DialogSettingsRecord> builder = modelBuilder.Entity<DialogSettingsRecord>();
        builder.ToTable("DialogSettings", table =>
        {
            table.HasCheckConstraint("CK_Settings_Version", mapping.Check("\"Version\" > 0"));
            table.HasCheckConstraint("CK_Settings_Model", mapping.Check("length(\"Model\") > 0"));
            table.HasCheckConstraint("CK_Settings_Effort", mapping.Check("length(\"Effort\") > 0"));
        });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.Version).IsConcurrencyToken();
        builder.Property(record => record.Model).HasColumnType(mapping.TextType).IsRequired();
        builder.Property(record => record.Effort).HasColumnType(mapping.TextType).IsRequired();
        builder.HasOne<DialogRecord>().WithOne().HasForeignKey<DialogSettingsRecord>(record => record.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
