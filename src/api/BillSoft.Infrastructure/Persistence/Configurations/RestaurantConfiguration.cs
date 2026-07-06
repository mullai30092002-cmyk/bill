using BillSoft.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");

        builder.HasKey(entity => entity.RestaurantId);

        builder.Property(entity => entity.RestaurantId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.BusinessType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(RestaurantBusinessType.Restaurant);

        builder.Property(entity => entity.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(entity => entity.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(entity => entity.TimeZoneId)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(entity => entity.RestaurantCode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(entity => entity.NormalizedRestaurantCode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(entity => entity.LegalName)
            .HasMaxLength(256);

        builder.Property(entity => entity.Phone)
            .HasMaxLength(32);

        builder.Property(entity => entity.Email)
            .HasMaxLength(256);

        builder.Property(entity => entity.Address)
            .HasMaxLength(512);

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => entity.NormalizedRestaurantCode)
            .IsUnique()
            .HasDatabaseName("UX_Restaurants_NormalizedRestaurantCode");
    }
}
