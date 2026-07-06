using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class PosOrderLineConfiguration : IEntityTypeConfiguration<PosOrderLine>
{
    public void Configure(EntityTypeBuilder<PosOrderLine> builder)
    {
        builder.ToTable("PosOrderLines");

        builder.HasKey(entity => entity.PosOrderLineId);

        builder.Property(entity => entity.PosOrderLineId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.PosOrderId)
            .IsRequired();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.MenuItemId)
            .IsRequired();

        builder.Property(entity => entity.MenuCategoryId)
            .IsRequired();

        builder.Property(entity => entity.MenuItemNameSnapshot)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.MenuCategoryNameSnapshot)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(entity => entity.SkuSnapshot)
            .HasMaxLength(64);

        builder.Property(entity => entity.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(entity => entity.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.LineSubtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.LineTax)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasMaxLength(300);

        builder.Property(entity => entity.DisplayOrder)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.PosOrderId)
            .HasDatabaseName("IX_PosOrderLines_PosOrderId");

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_PosOrderLines_RestaurantId");

        builder.HasIndex(entity => entity.MenuItemId)
            .HasDatabaseName("IX_PosOrderLines_MenuItemId");

        builder.HasIndex(entity => entity.MenuCategoryId)
            .HasDatabaseName("IX_PosOrderLines_MenuCategoryId");

        builder.HasOne<PosOrder>()
            .WithMany(order => order.PosOrderLines)
            .HasForeignKey(entity => entity.PosOrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PosOrderLines_PosOrders_PosOrderId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrderLines_Restaurants_RestaurantId");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrderLines_MenuItems_MenuItemId");

        builder.HasOne<MenuCategory>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrderLines_MenuCategories_MenuCategoryId");
    }
}
