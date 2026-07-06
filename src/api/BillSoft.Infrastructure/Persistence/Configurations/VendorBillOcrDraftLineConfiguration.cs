using BillSoft.Domain.Inventory;
using BillSoft.Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class VendorBillOcrDraftLineConfiguration : IEntityTypeConfiguration<VendorBillOcrDraftLine>
{
    public void Configure(EntityTypeBuilder<VendorBillOcrDraftLine> builder)
    {
        builder.ToTable("VendorBillOcrDraftLines");

        builder.HasKey(entity => entity.VendorBillOcrDraftLineId);

        builder.Property(entity => entity.VendorBillOcrDraftLineId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.VendorBillOcrDraftId)
            .IsRequired();

        builder.Property(entity => entity.LineNumber)
            .IsRequired();

        builder.Property(entity => entity.ExtractedDescription)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(entity => entity.ExtractedQuantity)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ExtractedUnitCost)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ExtractedLineTotal)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ConfidenceScore)
            .HasPrecision(5, 4);

        builder.Property(entity => entity.SelectedInventoryItemId);

        builder.Property(entity => entity.IsIgnored)
            .IsRequired();

        builder.Property(entity => entity.ReviewedDescription)
            .HasMaxLength(300);

        builder.Property(entity => entity.ReviewedQuantity)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ReviewedUnitCost)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ReviewedLineTotal)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_VendorBillOcrDraftLines_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.VendorBillOcrDraftId })
            .HasDatabaseName("IX_VendorBillOcrDraftLines_RestaurantId_BranchId_DraftId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.VendorBillOcrDraftId, entity.LineNumber })
            .IsUnique()
            .HasDatabaseName("UX_VendorBillOcrDraftLines_RestaurantId_DraftId_LineNumber");

        builder.HasOne<VendorBillOcrDraft>()
            .WithMany(entity => entity.Lines)
            .HasForeignKey(entity => entity.VendorBillOcrDraftId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_VendorBillOcrDraftLines_VendorBillOcrDrafts_VendorBillOcrDraftId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.SelectedInventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillOcrDraftLines_InventoryItems_SelectedInventoryItemId");
    }
}
