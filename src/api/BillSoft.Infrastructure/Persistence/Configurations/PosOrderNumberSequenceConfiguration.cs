using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class PosOrderNumberSequenceConfiguration : IEntityTypeConfiguration<PosOrderNumberSequence>
{
    public void Configure(EntityTypeBuilder<PosOrderNumberSequence> builder)
    {
        builder.ToTable("PosOrderNumberSequences");

        builder.HasKey(entity => entity.PosOrderNumberSequenceId);

        builder.Property(entity => entity.PosOrderNumberSequenceId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.OrderDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.LastSequence)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.OrderDate })
            .IsUnique()
            .HasDatabaseName("UX_PosOrderNumberSequences_RestaurantId_BranchId_OrderDate");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrderNumberSequences_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PosOrderNumberSequences_Branches_BranchId");
    }
}
