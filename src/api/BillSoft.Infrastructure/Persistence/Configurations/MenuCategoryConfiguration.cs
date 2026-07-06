using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("MenuCategories");

        builder.HasKey(entity => entity.MenuCategoryId);

        builder.Property(entity => entity.MenuCategoryId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(entity => entity.DisplayOrder)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_MenuCategories_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Name })
            .IsUnique()
            .HasDatabaseName("UX_MenuCategories_RestaurantId_Name");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuCategories_Restaurants_RestaurantId");
    }
}
