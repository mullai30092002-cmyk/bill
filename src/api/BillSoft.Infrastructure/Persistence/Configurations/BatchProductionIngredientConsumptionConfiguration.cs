using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BatchProductionIngredientConsumptionConfiguration : IEntityTypeConfiguration<BatchProductionIngredientConsumption>
{
    public void Configure(EntityTypeBuilder<BatchProductionIngredientConsumption> builder)
    {
        builder.ToTable("BatchProductionIngredientConsumptions");

        builder.HasKey(entity => entity.BatchProductionIngredientConsumptionId);

        builder.Property(entity => entity.BatchProductionIngredientConsumptionId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.BatchProductionId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.InventoryMovementId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.QuantityConsumed)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_BatchProductionIngredientConsumptions_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_BatchProductionIngredientConsumptions_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BatchProductionId, entity.InventoryItemId })
            .IsUnique()
            .HasDatabaseName("UX_BatchProductionIngredientConsumptions_RestaurantId_BatchProductionId_InventoryItemId");

        builder.HasIndex(entity => entity.InventoryMovementId)
            .HasDatabaseName("IX_BatchProductionIngredientConsumptions_InventoryMovementId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductionIngredientConsumptions_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductionIngredientConsumptions_Branches_BranchId");

        builder.HasOne<BatchProduction>()
            .WithMany()
            .HasForeignKey(entity => entity.BatchProductionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductionIngredientConsumptions_BatchProductions_BatchProductionId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductionIngredientConsumptions_InventoryItems_InventoryItemId");

        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductionIngredientConsumptions_InventoryMovements_InventoryMovementId");
    }
}
