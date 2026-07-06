using BillSoft.Domain.Billing;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class PaymentNumberSequenceConfiguration : IEntityTypeConfiguration<PaymentNumberSequence>
{
    public void Configure(EntityTypeBuilder<PaymentNumberSequence> builder)
    {
        builder.ToTable("PaymentNumberSequences");

        builder.HasKey(entity => entity.PaymentNumberSequenceId);

        builder.Property(entity => entity.PaymentNumberSequenceId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.PaymentDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.LastSequence)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.PaymentDate })
            .IsUnique()
            .HasDatabaseName("UX_PaymentNumberSequences_RestaurantId_BranchId_PaymentDate");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PaymentNumberSequences_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PaymentNumberSequences_Branches_BranchId");
    }
}
