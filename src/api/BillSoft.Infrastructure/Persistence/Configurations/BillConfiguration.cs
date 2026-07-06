using BillSoft.Domain.Billing;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bills");

        builder.HasKey(entity => entity.BillId);

        builder.Property(entity => entity.BillId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.PosOrderId)
            .IsRequired();

        builder.Property(entity => entity.BillNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(entity => entity.BusinessDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Subtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.TaxTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.GrandTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.AmountPaid)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.BalanceDue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.CreatedByUserId);

        builder.Property(entity => entity.CancelledByUserId);

        builder.Property(entity => entity.CancelledAt);

        builder.Property(entity => entity.CancelReason)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_Bills_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_Bills_RestaurantId_BranchId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.BusinessDate })
            .HasDatabaseName("IX_Bills_RestaurantId_BranchId_BusinessDate");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.BillNumber })
            .IsUnique()
            .HasDatabaseName("UX_Bills_RestaurantId_BranchId_BillNumber");

        builder.HasIndex(entity => entity.PosOrderId)
            .HasDatabaseName("IX_Bills_PosOrderId");

        builder.HasIndex(entity => entity.PosOrderId)
            .IsUnique()
            .HasFilter("[Status] <> 'Cancelled'")
            .HasDatabaseName("UX_Bills_PosOrderId_Active");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Status })
            .HasDatabaseName("IX_Bills_RestaurantId_Status");

        builder.HasIndex(entity => entity.CreatedAt)
            .HasDatabaseName("IX_Bills_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Bills_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Bills_Branches_BranchId");

        builder.HasOne<PosOrder>()
            .WithMany()
            .HasForeignKey(entity => entity.PosOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Bills_PosOrders_PosOrderId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Bills_Users_CreatedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Bills_Users_CancelledByUserId");
    }
}
