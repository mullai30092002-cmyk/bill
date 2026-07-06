using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class InventoryLotAllocationConfiguration : IEntityTypeConfiguration<InventoryLotAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryLotAllocation> builder)
    {
        builder.ToTable("InventoryLotAllocations");

        builder.HasKey(entity => entity.InventoryLotAllocationId);

        builder.Property(entity => entity.InventoryLotAllocationId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.InventoryLotId)
            .IsRequired();

        builder.Property(entity => entity.InventoryMovementId)
            .IsRequired();

        builder.Property(entity => entity.QuantityAllocated)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.AllocationReason)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.InventoryItemId })
            .HasDatabaseName("IX_InventoryLotAllocations_RestaurantId_BranchId_InventoryItemId");

        builder.HasIndex(entity => entity.InventoryLotId)
            .HasDatabaseName("IX_InventoryLotAllocations_InventoryLotId");

        builder.HasIndex(entity => entity.InventoryMovementId)
            .HasDatabaseName("IX_InventoryLotAllocations_InventoryMovementId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLotAllocations_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLotAllocations_Branches_BranchId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLotAllocations_InventoryItems_InventoryItemId");

        builder.HasOne<InventoryLot>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryLotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLotAllocations_InventoryLots_InventoryLotId");

        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLotAllocations_InventoryMovements_InventoryMovementId");
    }
}
