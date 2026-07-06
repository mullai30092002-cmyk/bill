using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class KitchenTicketConfiguration : IEntityTypeConfiguration<KitchenTicket>
{
    public void Configure(EntityTypeBuilder<KitchenTicket> builder)
    {
        builder.ToTable("KitchenTickets");

        builder.HasKey(entity => entity.KitchenTicketId);

        builder.Property(entity => entity.KitchenTicketId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.PosOrderId)
            .IsRequired();

        builder.Property(entity => entity.TicketNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.OrderNumberSnapshot)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(entity => entity.OrderTypeSnapshot)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(entity => entity.TableNameSnapshot)
            .HasMaxLength(100);

        builder.Property(entity => entity.CustomerNameSnapshot)
            .HasMaxLength(200);

        builder.Property(entity => entity.OrderNotesSnapshot)
            .HasMaxLength(1000);

        builder.Property(entity => entity.CreatedByUserId);

        builder.Property(entity => entity.LastStatusChangedByUserId);

        builder.Property(entity => entity.CancelledByUserId);

        builder.Property(entity => entity.CancelledAt);

        builder.Property(entity => entity.CancelReason)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.Property(entity => entity.PreparingAt);

        builder.Property(entity => entity.ReadyAt);

        builder.Property(entity => entity.ServedAt);

        builder.Property(entity => entity.InventoryDeductionStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_KitchenTickets_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_KitchenTickets_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.TicketNumber })
            .IsUnique()
            .HasDatabaseName("UX_KitchenTickets_RestaurantId_BranchId_TicketNumber");

        builder.HasIndex(entity => entity.PosOrderId)
            .HasDatabaseName("IX_KitchenTickets_PosOrderId");

        builder.HasIndex(entity => entity.PosOrderId)
            .IsUnique()
            .HasFilter("[Status] <> 'Cancelled'")
            .HasDatabaseName("UX_KitchenTickets_PosOrderId_Active");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Status })
            .HasDatabaseName("IX_KitchenTickets_RestaurantId_Status");

        builder.HasIndex(entity => entity.CreatedAt)
            .HasDatabaseName("IX_KitchenTickets_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTickets_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTickets_Branches_BranchId");

        builder.HasOne<PosOrder>()
            .WithMany()
            .HasForeignKey(entity => entity.PosOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTickets_PosOrders_PosOrderId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTickets_Users_CreatedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.LastStatusChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTickets_Users_LastStatusChangedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTickets_Users_CancelledByUserId");
    }
}
