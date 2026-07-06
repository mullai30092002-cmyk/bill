using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");

        builder.HasKey(entity => entity.MenuItemId);

        builder.Property(entity => entity.MenuItemId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.MenuCategoryId)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.Description)
            .HasMaxLength(500);

        builder.Property(entity => entity.Sku)
            .HasMaxLength(64);

        builder.Property(entity => entity.BasePrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(entity => entity.IsVegetarian)
            .IsRequired();

        builder.Property(entity => entity.IsAvailableForEatIn)
            .IsRequired();

        builder.Property(entity => entity.IsAvailableForParcel)
            .IsRequired();

        builder.Property(entity => entity.InventoryDeductionMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(MenuItemInventoryDeductionMode.RecipeOnServe)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_MenuItems_RestaurantId");

        builder.HasIndex(entity => entity.MenuCategoryId)
            .HasDatabaseName("IX_MenuItems_MenuCategoryId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.MenuCategoryId, entity.Name })
            .IsUnique()
            .HasDatabaseName("UX_MenuItems_RestaurantId_MenuCategoryId_Name");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Sku })
            .IsUnique()
            .HasDatabaseName("UX_MenuItems_RestaurantId_Sku")
            .HasFilter("[Sku] IS NOT NULL");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItems_Restaurants_RestaurantId");

        builder.HasOne<MenuCategory>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItems_MenuCategories_MenuCategoryId");
    }
}
