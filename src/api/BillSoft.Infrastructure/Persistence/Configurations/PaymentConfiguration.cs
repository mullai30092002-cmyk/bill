using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(entity => entity.PaymentId);

        builder.Property(entity => entity.PaymentId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.BillId)
            .IsRequired();

        builder.Property(entity => entity.CashierShiftId);

        builder.Property(entity => entity.PaymentNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(entity => entity.PaymentMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.ReferenceNumber)
            .HasMaxLength(120);

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.RecordedByUserId);

        builder.Property(entity => entity.CancelledByUserId);

        builder.Property(entity => entity.CancelledAt);

        builder.Property(entity => entity.CancelReason)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_Payments_RestaurantId");

        builder.HasIndex(entity => entity.BranchId)
            .HasDatabaseName("IX_Payments_BranchId");

        builder.HasIndex(entity => entity.BillId)
            .HasDatabaseName("IX_Payments_BillId");

        builder.HasIndex(entity => entity.CashierShiftId)
            .HasDatabaseName("IX_Payments_CashierShiftId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.PaymentNumber })
            .IsUnique()
            .HasDatabaseName("UX_Payments_RestaurantId_BranchId_PaymentNumber");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Status })
            .HasDatabaseName("IX_Payments_RestaurantId_Status");

        builder.HasIndex(entity => entity.CreatedAt)
            .HasDatabaseName("IX_Payments_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Payments_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Payments_Branches_BranchId");

        builder.HasOne<Bill>()
            .WithMany(entity => entity.Payments)
            .HasForeignKey(entity => entity.BillId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Payments_Bills_BillId");

        builder.HasOne<CashierShift>()
            .WithMany(entity => entity.Payments)
            .HasForeignKey(entity => entity.CashierShiftId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Payments_CashierShifts_CashierShiftId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Payments_Users_RecordedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Payments_Users_CancelledByUserId");
    }
}
