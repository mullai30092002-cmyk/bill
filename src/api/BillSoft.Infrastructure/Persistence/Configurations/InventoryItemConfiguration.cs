using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");

        builder.HasKey(entity => entity.InventoryItemId);

        builder.Property(entity => entity.InventoryItemId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.NormalizedName)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.Category)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.UnitOfMeasure)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(entity => entity.LowStockThreshold)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_InventoryItems_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_InventoryItems_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.NormalizedName })
            .IsUnique()
            .HasDatabaseName("UX_InventoryItems_RestaurantId_BranchId_NormalizedName");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryItems_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventoryItems_Branches_BranchId");
    }
}
