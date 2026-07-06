using BillSoft.Domain.Billing;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BillPrintEventConfiguration : IEntityTypeConfiguration<BillPrintEvent>
{
    public void Configure(EntityTypeBuilder<BillPrintEvent> builder)
    {
        builder.ToTable("BillPrintEvents");

        builder.HasKey(entity => entity.BillPrintEventId);

        builder.Property(entity => entity.BillPrintEventId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.BillId)
            .IsRequired();

        builder.Property(entity => entity.PrintedByUserId);

        builder.Property(entity => entity.PrintSequence)
            .IsRequired();

        builder.Property(entity => entity.PrintReason)
            .HasMaxLength(300);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_BillPrintEvents_RestaurantId");

        builder.HasIndex(entity => entity.BranchId)
            .HasDatabaseName("IX_BillPrintEvents_BranchId");

        builder.HasIndex(entity => entity.BillId)
            .HasDatabaseName("IX_BillPrintEvents_BillId");

        builder.HasIndex(entity => new { entity.BillId, entity.PrintSequence })
            .IsUnique()
            .HasDatabaseName("UX_BillPrintEvents_BillId_PrintSequence");

        builder.HasIndex(entity => entity.CreatedAt)
            .HasDatabaseName("IX_BillPrintEvents_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillPrintEvents_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillPrintEvents_Branches_BranchId");

        builder.HasOne<Bill>()
            .WithMany(entity => entity.PrintEvents)
            .HasForeignKey(entity => entity.BillId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_BillPrintEvents_Bills_BillId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.PrintedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillPrintEvents_Users_PrintedByUserId");
    }
}
