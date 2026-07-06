using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");

        builder.HasKey(entity => entity.InventoryMovementId);

        builder.Property(entity => entity.InventoryMovementId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.MovementType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Quantity)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.UnitCost)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ReferenceNumber)
            .HasMaxLength(128);

        builder.Property(entity => entity.Reason)
            .HasMaxLength(128);

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.ExpiresAtUtc);

        builder.Property(entity => entity.BatchReference)
            .HasMaxLength(128);

        builder.Property(entity => entity.MovementDate)
            .IsRequired();

        builder.Property(entity => entity.RecordedByUserId)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_InventoryMovements_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.CreatedAtUtc })
            .HasDatabaseName("IX_InventoryMovements_RestaurantId_BranchId_CreatedAtUtc");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.InventoryItemId, entity.MovementDate })
            .HasDatabaseName("IX_InventoryMovements_RestaurantId_InventoryItemId_MovementDate");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryMovements_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryMovements_Branches_BranchId");

        builder.HasOne<InventoryItem>()
            .WithMany(item => item.Movements)
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryMovements_InventoryItems_InventoryItemId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryMovements_Users_RecordedByUserId");
    }
}
