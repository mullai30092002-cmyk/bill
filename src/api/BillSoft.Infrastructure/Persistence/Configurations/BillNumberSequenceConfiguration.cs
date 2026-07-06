using BillSoft.Domain.Billing;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BillNumberSequenceConfiguration : IEntityTypeConfiguration<BillNumberSequence>
{
    public void Configure(EntityTypeBuilder<BillNumberSequence> builder)
    {
        builder.ToTable("BillNumberSequences");

        builder.HasKey(entity => entity.BillNumberSequenceId);

        builder.Property(entity => entity.BillNumberSequenceId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.BillDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.LastSequence)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.BillDate })
            .IsUnique()
            .HasDatabaseName("UX_BillNumberSequences_RestaurantId_BranchId_BillDate");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillNumberSequences_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BillNumberSequences_Branches_BranchId");
    }
}
