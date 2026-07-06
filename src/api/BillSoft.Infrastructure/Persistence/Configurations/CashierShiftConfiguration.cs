using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class CashierShiftConfiguration : IEntityTypeConfiguration<CashierShift>
{
    public void Configure(EntityTypeBuilder<CashierShift> builder)
    {
        builder.ToTable("CashierShifts");

        builder.HasKey(entity => entity.CashierShiftId);

        builder.Property(entity => entity.CashierShiftId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.OpenedByUserId)
            .IsRequired();

        builder.Property(entity => entity.ClosedByUserId);

        builder.Property(entity => entity.BusinessDate)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.OpeningCashAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.ExpectedCashAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.CountedCashAmount)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.CashVarianceAmount)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.OpenedAt)
            .IsRequired();

        builder.Property(entity => entity.ClosedAt);

        builder.Property(entity => entity.OpeningNote)
            .HasMaxLength(500);

        builder.Property(entity => entity.ClosingNote)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_CashierShifts_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.Status })
            .HasDatabaseName("IX_CashierShifts_RestaurantId_BranchId_Status");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.BusinessDate })
            .HasDatabaseName("IX_CashierShifts_RestaurantId_BranchId_BusinessDate");

        builder.HasIndex(entity => entity.OpenedByUserId)
            .HasDatabaseName("IX_CashierShifts_OpenedByUserId");

        builder.HasIndex(entity => entity.OpenedAt)
            .HasDatabaseName("IX_CashierShifts_OpenedAt");

        builder.HasIndex(entity => entity.ClosedAt)
            .HasDatabaseName("IX_CashierShifts_ClosedAt");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.OpenedByUserId })
            .IsUnique()
            .HasFilter("[Status] = 'Open'")
            .HasDatabaseName("UX_CashierShifts_RestaurantId_BranchId_OpenedByUserId_Open");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashierShifts_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashierShifts_Branches_BranchId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashierShifts_Users_OpenedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashierShifts_Users_ClosedByUserId");
    }
}
