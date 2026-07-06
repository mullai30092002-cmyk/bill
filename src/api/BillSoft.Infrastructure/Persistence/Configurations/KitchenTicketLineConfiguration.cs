using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class KitchenTicketLineConfiguration : IEntityTypeConfiguration<KitchenTicketLine>
{
    public void Configure(EntityTypeBuilder<KitchenTicketLine> builder)
    {
        builder.ToTable("KitchenTicketLines");

        builder.HasKey(entity => entity.KitchenTicketLineId);

        builder.Property(entity => entity.KitchenTicketLineId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.KitchenTicketId)
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

        builder.Property(entity => entity.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasMaxLength(300);

        builder.Property(entity => entity.DisplayOrder)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.HasIndex(entity => entity.KitchenTicketId)
            .HasDatabaseName("IX_KitchenTicketLines_KitchenTicketId");

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_KitchenTicketLines_RestaurantId");

        builder.HasIndex(entity => entity.PosOrderLineId)
            .HasDatabaseName("IX_KitchenTicketLines_PosOrderLineId");

        builder.HasOne<KitchenTicket>()
            .WithMany(entity => entity.KitchenTicketLines)
            .HasForeignKey(entity => entity.KitchenTicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_KitchenTicketLines_KitchenTickets_KitchenTicketId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketLines_Restaurants_RestaurantId");

        builder.HasOne<PosOrderLine>()
            .WithMany()
            .HasForeignKey(entity => entity.PosOrderLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketLines_PosOrderLines_PosOrderLineId");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketLines_MenuItems_MenuItemId");

        builder.HasOne<MenuCategory>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketLines_MenuCategories_MenuCategoryId");
    }
}
