using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");

        builder.HasKey(entity => entity.VendorId);

        builder.Property(entity => entity.VendorId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId);

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.NormalizedName)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.VendorType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.ContactName)
            .HasMaxLength(160);

        builder.Property(entity => entity.MobileNumber)
            .HasMaxLength(32);

        builder.Property(entity => entity.NormalizedMobileNumber)
            .HasMaxLength(32);

        builder.Property(entity => entity.Address)
            .HasMaxLength(512);

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.IsActive)
            .IsRequired();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_Vendors_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.NormalizedName })
            .IsUnique()
            .HasFilter("[BranchId] IS NOT NULL")
            .HasDatabaseName("UX_Vendors_RestaurantId_BranchId_NormalizedName");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.NormalizedName })
            .IsUnique()
            .HasFilter("[BranchId] IS NULL")
            .HasDatabaseName("UX_Vendors_RestaurantId_NormalizedName");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.NormalizedMobileNumber })
            .IsUnique()
            .HasFilter("[NormalizedMobileNumber] IS NOT NULL")
            .HasDatabaseName("UX_Vendors_RestaurantId_NormalizedMobileNumber");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Vendors_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Vendors_Branches_BranchId");
    }
}
