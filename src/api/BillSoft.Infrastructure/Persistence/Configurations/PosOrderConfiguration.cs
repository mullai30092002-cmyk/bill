using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class PosOrderConfiguration : IEntityTypeConfiguration<PosOrder>
{
    public void Configure(EntityTypeBuilder<PosOrder> builder)
    {
        builder.ToTable("PosOrders");

        builder.HasKey(entity => entity.PosOrderId);

        builder.Property(entity => entity.PosOrderId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.OrderNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(entity => entity.OrderType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.TableName)
            .HasMaxLength(80);

        builder.Property(entity => entity.CustomerName)
            .HasMaxLength(160);

        builder.Property(entity => entity.CustomerMobile)
            .HasMaxLength(32);

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.Subtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.TaxTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.GrandTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.ConfirmedAt);

        builder.Property(entity => entity.CancelledAt);

        builder.Property(entity => entity.CancelReason)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedByUserId);

        builder.Property(entity => entity.ConfirmedByUserId);

        builder.Property(entity => entity.CancelledByUserId);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_PosOrders_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_PosOrders_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.OrderNumber })
            .IsUnique()
            .HasDatabaseName("UX_PosOrders_RestaurantId_BranchId_OrderNumber");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Status })
            .HasDatabaseName("IX_PosOrders_RestaurantId_Status");

        builder.HasIndex(entity => entity.CreatedAt)
            .HasDatabaseName("IX_PosOrders_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrders_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrders_Branches_BranchId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrders_Users_CreatedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrders_Users_ConfirmedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrders_Users_CancelledByUserId");
    }
}
