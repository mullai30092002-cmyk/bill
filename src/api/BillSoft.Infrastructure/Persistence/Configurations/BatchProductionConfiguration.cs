using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BatchProductionConfiguration : IEntityTypeConfiguration<BatchProduction>
{
    public void Configure(EntityTypeBuilder<BatchProduction> builder)
    {
        builder.ToTable("BatchProductions");

        builder.HasKey(entity => entity.BatchProductionId);

        builder.Property(entity => entity.BatchProductionId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.MenuItemId)
            .IsRequired();

        builder.Property(entity => entity.PreparedInventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.QuantityProduced)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.BusinessDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.ProducedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.ProducedByUserId)
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.ShelfLifeHours)
            .HasPrecision(10, 2);

        builder.Property(entity => entity.ExpiresAtUtc);

        builder.Property(entity => entity.StorageNote)
            .HasMaxLength(500);

        builder.Property(entity => entity.BatchReference)
            .HasMaxLength(128);

        builder.Property(entity => entity.PreparedInventoryMovementId);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_BatchProductions_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.BusinessDate })
            .HasDatabaseName("IX_BatchProductions_RestaurantId_BranchId_BusinessDate");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.MenuItemId, entity.BusinessDate })
            .HasDatabaseName("IX_BatchProductions_RestaurantId_BranchId_MenuItemId_BusinessDate");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductions_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductions_Branches_BranchId");

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(entity => entity.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductions_MenuItems_MenuItemId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.PreparedInventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductions_InventoryItems_PreparedInventoryItemId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ProducedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BatchProductions_Users_ProducedByUserId");
    }
}
