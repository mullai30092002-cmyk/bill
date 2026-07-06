using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class VendorBillConfiguration : IEntityTypeConfiguration<VendorBill>
{
    public void Configure(EntityTypeBuilder<VendorBill> builder)
    {
        builder.ToTable("VendorBills");

        builder.HasKey(entity => entity.VendorBillId);

        builder.Property(entity => entity.VendorBillId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.VendorId)
            .IsRequired();

        builder.Property(entity => entity.BillNumber)
            .HasMaxLength(40);

        builder.Property(entity => entity.NormalizedBillNumber)
            .HasMaxLength(40);

        builder.Property(entity => entity.BillDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.DueDate)
            .HasColumnType("date");

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.PaidAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.BalanceAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.CancelledAtUtc);

        builder.Property(entity => entity.CancelledByUserId);

        builder.Property(entity => entity.CancellationReason)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_VendorBills_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.BillDate })
            .HasDatabaseName("IX_VendorBills_RestaurantId_BranchId_BillDate");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.Status })
            .HasDatabaseName("IX_VendorBills_RestaurantId_BranchId_Status");

        builder.HasIndex(entity => entity.VendorId)
            .HasDatabaseName("IX_VendorBills_VendorId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.VendorId, entity.NormalizedBillNumber })
            .IsUnique()
            .HasFilter("[NormalizedBillNumber] IS NOT NULL")
            .HasDatabaseName("UX_VendorBills_RestaurantId_VendorId_NormalizedBillNumber");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBills_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBills_Branches_BranchId");

        builder.HasOne<Vendor>()
            .WithMany(entity => entity.VendorBills)
            .HasForeignKey(entity => entity.VendorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBills_Vendors_VendorId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBills_Users_CancelledByUserId");
    }
}
