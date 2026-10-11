using AgentBridge.Persistence.EfCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Компактные library-owned tables, scope keyset/feed и append-only recovery operation keys.</summary>
internal static class CatalogModelMapping
{
    public static void Configure(ModelBuilder model, ProviderModelMapping mapping)
    {
        EntityTypeBuilder<DialogCatalogRecord> catalog = model.Entity<DialogCatalogRecord>();
        catalog.ToTable("DialogCatalog");
        catalog.HasKey(row => row.Id);
        catalog.Property(row => row.Id).ValueGeneratedNever();
        catalog.Property(row => row.Revision).IsConcurrencyToken();
        string collation = mapping.IsSqlServer ? "Latin1_General_100_BIN2" : mapping.OwnerCollation!;
        catalog.Property(row => row.ScopeKey).HasMaxLength(64).UseCollation(collation).IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        catalog.Property(row => row.IdSortKey).HasMaxLength(32).UseCollation(collation).IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        foreach (string name in new[] { nameof(DialogCatalogRecord.OwnerId), nameof(DialogCatalogRecord.SiteId),
            nameof(DialogCatalogRecord.AgentId), nameof(DialogCatalogRecord.ProfileJson), nameof(DialogCatalogRecord.PolicyJson) })
            catalog.Property<string>(name).HasColumnType(mapping.TextType).IsRequired()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        catalog.Property(row => row.IncarnationId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        catalog.Property(row => row.CreatedAtUtc).HasConversion<UtcTicksConverter>().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        catalog.Property(row => row.ExpiresAtUtc).HasConversion<UtcTicksConverter>().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        catalog.Property(row => row.LastMessageAtUtc).HasConversion<UtcTicksConverter>();
        catalog.Property(row => row.SortTimeUtc).HasConversion<UtcTicksConverter>();
        catalog.HasIndex(row => new { row.ScopeKey, row.SortTimeUtc, row.IdSortKey }).IsDescending(false, true, true);
        EntityTypeBuilder<DialogCatalogClockRecord> clock = model.Entity<DialogCatalogClockRecord>();
        clock.ToTable("DialogCatalogClocks");
        clock.HasKey(row => row.Id);
        clock.Property(row => row.Id).HasMaxLength(64).UseCollation(collation).ValueGeneratedNever();
        clock.Property(row => row.Sequence).IsConcurrencyToken();
        EntityTypeBuilder<DialogCatalogChangeRecord> change = model.Entity<DialogCatalogChangeRecord>();
        change.ToTable("DialogCatalogChanges");
        change.HasKey(row => new { row.ScopeKey, row.Sequence });
        change.Property(row => row.ScopeKey).HasMaxLength(64).UseCollation(collation);
        change.Property(row => row.Sequence).ValueGeneratedNever();
        change.Property(row => row.StateJson).HasColumnType(mapping.TextType).IsRequired();
        EntityTypeBuilder<DialogRecoveryOperationRecord> recovery = model.Entity<DialogRecoveryOperationRecord>();
        recovery.ToTable("DialogRecoveryOperations");
        recovery.HasKey(row => new { row.DialogId, row.Id });
        recovery.Property(row => row.Id).ValueGeneratedNever();
        recovery.Property(row => row.PayloadHash).HasMaxLength(64).UseCollation(collation);
        recovery.Property(row => row.ResultJson).HasColumnType(mapping.TextType).IsRequired();
        recovery.Property(row => row.PairsJson).HasColumnType(mapping.TextType).IsRequired();
        recovery.HasIndex(row => new { row.DialogId, row.Revision });
        recovery.HasOne<DialogRecord>().WithMany().HasForeignKey(row => row.DialogId).OnDelete(DeleteBehavior.Cascade);
    }
}
