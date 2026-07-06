using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class CashDrawerMovementConfiguration : IEntityTypeConfiguration<CashDrawerMovement>
{
    public void Configure(EntityTypeBuilder<CashDrawerMovement> builder)
    {
        builder.ToTable("CashDrawerMovements");

        builder.HasKey(entity => entity.CashDrawerMovementId);

        builder.Property(entity => entity.CashDrawerMovementId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.CashierShiftId)
            .IsRequired();

        builder.Property(entity => entity.MovementType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(entity => entity.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedByUserId)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.HasIndex(entity => entity.CashierShiftId)
            .HasDatabaseName("IX_CashDrawerMovements_CashierShiftId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId })
            .HasDatabaseName("IX_CashDrawerMovements_RestaurantId_BranchId");

        builder.HasIndex(entity => entity.MovementType)
            .HasDatabaseName("IX_CashDrawerMovements_MovementType");

        builder.HasIndex(entity => entity.CreatedAt)
            .HasDatabaseName("IX_CashDrawerMovements_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashDrawerMovements_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashDrawerMovements_Branches_BranchId");

        builder.HasOne<CashierShift>()
            .WithMany(entity => entity.CashDrawerMovements)
            .HasForeignKey(entity => entity.CashierShiftId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CashDrawerMovements_CashierShifts_CashierShiftId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CashDrawerMovements_Users_CreatedByUserId");
    }
}
