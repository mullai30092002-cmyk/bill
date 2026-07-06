using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class MenuItemPriceHistoryConfiguration : IEntityTypeConfiguration<MenuItemPriceHistory>
{
    public void Configure(EntityTypeBuilder<MenuItemPriceHistory> builder)
    {
        builder.ToTable("MenuItemPriceHistory");

        builder.HasKey(entity => entity.MenuItemPriceHistoryId);

        builder.Property(entity => entity.MenuItemPriceHistoryId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.MenuItemId)
            .IsRequired();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.OldPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.NewPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.ChangedByUserId);

        builder.Property(entity => entity.ChangedAt)
            .IsRequired();

        builder.Property(entity => entity.Reason)
            .HasMaxLength(500);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.MenuItemId })
            .HasDatabaseName("IX_MenuItemPriceHistory_RestaurantId_MenuItemId");

        builder.HasIndex(entity => new { entity.MenuItemId, entity.ChangedAt })
            .HasDatabaseName("IX_MenuItemPriceHistory_MenuItemId_ChangedAt");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemPriceHistory_MenuItems_MenuItemId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemPriceHistory_Restaurants_RestaurantId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MenuItemPriceHistory_Users_ChangedByUserId");
    }
}
