using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class MenuItemStockItemConfiguration : IEntityTypeConfiguration<MenuItemStockItem>
{
    public void Configure(EntityTypeBuilder<MenuItemStockItem> builder)
    {
        builder.ToTable("MenuItemStockItems");

        builder.HasKey(entity => entity.MenuItemStockItemId);

        builder.Property(entity => entity.MenuItemStockItemId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.MenuItemId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_MenuItemStockItems_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_MenuItemStockItems_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.MenuItemId })
            .IsUnique()
            .HasDatabaseName("UX_MenuItemStockItems_RestaurantId_BranchId_MenuItemId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemStockItems_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemStockItems_Branches_BranchId");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemStockItems_MenuItems_MenuItemId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemStockItems_InventoryItems_InventoryItemId");
    }
}
