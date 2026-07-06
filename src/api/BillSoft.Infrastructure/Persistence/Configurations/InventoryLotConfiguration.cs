using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class InventoryLotConfiguration : IEntityTypeConfiguration<InventoryLot>
{
    public void Configure(EntityTypeBuilder<InventoryLot> builder)
    {
        builder.ToTable("InventoryLots");

        builder.HasKey(entity => entity.InventoryLotId);

        builder.Property(entity => entity.InventoryLotId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.SourceMovementId);

        builder.Property(entity => entity.SourceBatchProductionId);

        builder.Property(entity => entity.BatchReference)
            .HasMaxLength(128);

        builder.Property(entity => entity.ReceivedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.ExpiresAtUtc);

        builder.Property(entity => entity.InitialQuantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.RemainingQuantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.UnitOfMeasure)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.Property(entity => entity.ClosedAtUtc);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.InventoryItemId })
            .HasDatabaseName("IX_InventoryLots_RestaurantId_BranchId_InventoryItemId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.InventoryItemId, entity.ExpiresAtUtc })
            .HasDatabaseName("IX_InventoryLots_RestaurantId_BranchId_InventoryItemId_ExpiresAtUtc");

        builder.HasIndex(entity => entity.SourceMovementId)
            .HasDatabaseName("IX_InventoryLots_SourceMovementId");

        builder.HasIndex(entity => entity.SourceBatchProductionId)
            .HasDatabaseName("IX_InventoryLots_SourceBatchProductionId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.InventoryItemId, entity.BatchReference })
            .HasDatabaseName("IX_InventoryLots_RestaurantId_BranchId_InventoryItemId_BatchReference");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLots_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLots_Branches_BranchId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLots_InventoryItems_InventoryItemId");

        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(entity => entity.SourceMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLots_InventoryMovements_SourceMovementId");

        builder.HasOne<BatchProduction>()
            .WithMany()
            .HasForeignKey(entity => entity.SourceBatchProductionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryLots_BatchProductions_SourceBatchProductionId");
    }
}
