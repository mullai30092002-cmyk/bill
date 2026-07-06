using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class VendorSettlementConfiguration : IEntityTypeConfiguration<VendorSettlement>
{
    public void Configure(EntityTypeBuilder<VendorSettlement> builder)
    {
        builder.ToTable("VendorSettlements");

        builder.HasKey(entity => entity.VendorSettlementId);

        builder.Property(entity => entity.VendorSettlementId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.VendorBillId)
            .IsRequired();

        builder.Property(entity => entity.PaymentMode)
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

        builder.Property(entity => entity.PaidAtUtc)
            .IsRequired();

        builder.Property(entity => entity.RecordedByUserId)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.PreviousOutstandingAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.NewOutstandingAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.CancelledAtUtc);

        builder.Property(entity => entity.CancelledByUserId);

        builder.Property(entity => entity.CancellationReason)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_VendorSettlements_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.VendorBillId })
            .HasDatabaseName("IX_VendorSettlements_RestaurantId_BranchId_VendorBillId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Status })
            .HasDatabaseName("IX_VendorSettlements_RestaurantId_Status");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorSettlements_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorSettlements_Branches_BranchId");

        builder.HasOne<VendorBill>()
            .WithMany(entity => entity.Settlements)
            .HasForeignKey(entity => entity.VendorBillId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorSettlements_VendorBills_VendorBillId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorSettlements_Users_RecordedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorSettlements_Users_CancelledByUserId");
    }
}
