using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");

        builder.HasKey(entity => entity.BranchId);

        builder.Property(entity => entity.BranchId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.Address)
            .HasMaxLength(512);

        builder.Property(entity => entity.Phone)
            .HasMaxLength(32);

        builder.Property(entity => entity.NormalizedPhone)
            .HasMaxLength(32);

        builder.Property(entity => entity.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(entity => entity.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(entity => entity.TimeZoneId)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Name })
            .IsUnique()
            .HasDatabaseName("UX_Branches_RestaurantId_Name");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.NormalizedPhone })
            .IsUnique()
            .HasFilter("[NormalizedPhone] IS NOT NULL")
            .HasDatabaseName("UX_Branches_RestaurantId_NormalizedPhone");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Branches_Restaurants_RestaurantId");
    }
}
