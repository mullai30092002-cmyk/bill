using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class KitchenTicketNumberSequenceConfiguration : IEntityTypeConfiguration<KitchenTicketNumberSequence>
{
    public void Configure(EntityTypeBuilder<KitchenTicketNumberSequence> builder)
    {
        builder.ToTable("KitchenTicketNumberSequences");

        builder.HasKey(entity => entity.KitchenTicketNumberSequenceId);

        builder.Property(entity => entity.KitchenTicketNumberSequenceId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.TicketDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(entity => entity.LastSequence)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.TicketDate })
            .IsUnique()
            .HasDatabaseName("UX_KitchenTicketNumberSequences_RestaurantId_BranchId_TicketDate");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketNumberSequences_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KitchenTicketNumberSequences_Branches_BranchId");
    }
}
