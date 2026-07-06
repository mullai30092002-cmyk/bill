using BillSoft.Domain.Inventory;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class KitchenTicketInventoryDeductionConfiguration : IEntityTypeConfiguration<KitchenTicketInventoryDeduction>
{
    public void Configure(EntityTypeBuilder<KitchenTicketInventoryDeduction> builder)
    {
        builder.ToTable("KitchenTicketInventoryDeductions");

        builder.HasKey(entity => entity.KitchenTicketInventoryDeductionId);

        builder.Property(entity => entity.KitchenTicketInventoryDeductionId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.KitchenTicketId)
            .IsRequired();

        builder.Property(entity => entity.InventoryItemId)
            .IsRequired();

        builder.Property(entity => entity.InventoryMovementId)
            .IsRequired();

        builder.Property(entity => entity.QuantityDeducted)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_KitchenTicketInventoryDeductions_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_KitchenTicketInventoryDeductions_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.KitchenTicketId, entity.InventoryItemId })
            .IsUnique()
            .HasDatabaseName("UX_KitchenTicketInventoryDeductions_RestaurantId_KitchenTicketId_InventoryItemId");

        builder.HasIndex(entity => entity.InventoryMovementId)
            .HasDatabaseName("IX_KitchenTicketInventoryDeductions_InventoryMovementId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketInventoryDeductions_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketInventoryDeductions_Branches_BranchId");

        builder.HasOne<KitchenTicket>()
            .WithMany()
            .HasForeignKey(entity => entity.KitchenTicketId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketInventoryDeductions_KitchenTickets_KitchenTicketId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketInventoryDeductions_InventoryItems_InventoryItemId");

        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(entity => entity.InventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketInventoryDeductions_InventoryMovements_InventoryMovementId");
    }
}
