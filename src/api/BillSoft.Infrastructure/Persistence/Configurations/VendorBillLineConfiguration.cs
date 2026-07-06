using BillSoft.Domain.Inventory;
using BillSoft.Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class VendorBillLineConfiguration : IEntityTypeConfiguration<VendorBillLine>
{
    public void Configure(EntityTypeBuilder<VendorBillLine> builder)
    {
        builder.ToTable("VendorBillLines");

        builder.HasKey(entity => entity.VendorBillLineId);

        builder.Property(entity => entity.VendorBillLineId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.VendorBillId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId);

        builder.Property(entity => entity.InventoryMovementId);

        builder.Property(entity => entity.Description)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(entity => entity.Quantity)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.UnitCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_VendorBillLines_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.VendorBillId })
            .HasDatabaseName("IX_VendorBillLines_RestaurantId_BranchId_VendorBillId");

        builder.HasIndex(entity => entity.InventoryMovementId)
            .IsUnique()
            .HasFilter("[InventoryMovementId] IS NOT NULL")
            .HasDatabaseName("UX_VendorBillLines_InventoryMovementId");

        builder.HasOne<VendorBill>()
            .WithMany(entity => entity.Lines)
            .HasForeignKey(entity => entity.VendorBillId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillLines_VendorBills_VendorBillId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillLines_InventoryItems_InventoryItemId");

        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillLines_InventoryMovements_InventoryMovementId");
    }
}
