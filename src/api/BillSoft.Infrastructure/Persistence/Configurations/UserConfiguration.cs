using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(entity => entity.UserId);

        builder.Property(entity => entity.UserId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId);

        builder.Property(entity => entity.FullName)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.MobileNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(entity => entity.MobileCountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(entity => entity.MobileDialCode)
            .IsRequired()
            .HasMaxLength(6);

        builder.Property(entity => entity.MobileNationalNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(entity => entity.MobileE164)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(entity => entity.Email)
            .HasMaxLength(256);

        builder.Property(entity => entity.NormalizedEmail)
            .HasMaxLength(256);

        builder.Property(entity => entity.PinHash)
            .HasMaxLength(512);

        builder.Property(entity => entity.PasswordHash)
            .HasMaxLength(512);

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt);

        builder.HasIndex(entity => new { entity.RestaurantId, entity.MobileE164 })
            .IsUnique()
            .HasDatabaseName("UX_Users_RestaurantId_MobileE164");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.NormalizedEmail })
            .IsUnique()
            .HasDatabaseName("UX_Users_RestaurantId_NormalizedEmail")
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Users_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Users_Branches_BranchId");
    }
}
