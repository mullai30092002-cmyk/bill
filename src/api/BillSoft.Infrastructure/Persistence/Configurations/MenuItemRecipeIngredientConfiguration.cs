using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class MenuItemRecipeIngredientConfiguration : IEntityTypeConfiguration<MenuItemRecipeIngredient>
{
    public void Configure(EntityTypeBuilder<MenuItemRecipeIngredient> builder)
    {
        builder.ToTable("MenuItemRecipeIngredients");

        builder.HasKey(entity => entity.MenuItemRecipeIngredientId);

        builder.Property(entity => entity.MenuItemRecipeIngredientId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.MenuItemId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.QuantityRequired)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_MenuItemRecipeIngredients_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_MenuItemRecipeIngredients_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.MenuItemId })
            .HasDatabaseName("IX_MenuItemRecipeIngredients_RestaurantId_BranchId_MenuItemId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.MenuItemId, entity.InventoryItemId })
            .IsUnique()
            .HasDatabaseName("UX_MenuItemRecipeIngredients_RestaurantId_BranchId_MenuItemId_InventoryItemId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemRecipeIngredients_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemRecipeIngredients_Branches_BranchId");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemRecipeIngredients_MenuItems_MenuItemId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemRecipeIngredients_InventoryItems_InventoryItemId");
    }
}
