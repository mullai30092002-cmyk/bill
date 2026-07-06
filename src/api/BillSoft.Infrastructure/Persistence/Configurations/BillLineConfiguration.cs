using BillSoft.Domain.Billing;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BillLineConfiguration : IEntityTypeConfiguration<BillLine>
{
    public void Configure(EntityTypeBuilder<BillLine> builder)
    {
        builder.ToTable("BillLines");

        builder.HasKey(entity => entity.BillLineId);

        builder.Property(entity => entity.BillLineId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.BillId)
            .IsRequired();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.PosOrderLineId)
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

        builder.HasIndex(entity => entity.BillId)
            .HasDatabaseName("IX_BillLines_BillId");

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_BillLines_RestaurantId");

        builder.HasIndex(entity => entity.PosOrderLineId)
            .HasDatabaseName("IX_BillLines_PosOrderLineId");

        builder.HasIndex(entity => entity.MenuItemId)
            .HasDatabaseName("IX_BillLines_MenuItemId");

        builder.HasIndex(entity => entity.MenuCategoryId)
            .HasDatabaseName("IX_BillLines_MenuCategoryId");

        builder.HasOne<Bill>()
            .WithMany(entity => entity.BillLines)
            .HasForeignKey(entity => entity.BillId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_BillLines_Bills_BillId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillLines_Restaurants_RestaurantId");

        builder.HasOne<PosOrderLine>()
            .WithMany()
            .HasForeignKey(entity => entity.PosOrderLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillLines_PosOrderLines_PosOrderLineId");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillLines_MenuItems_MenuItemId");

        builder.HasOne<MenuCategory>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillLines_MenuCategories_MenuCategoryId");
    }
}
