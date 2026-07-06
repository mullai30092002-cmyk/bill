using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(entity => entity.RefreshTokenId);

        builder.Property(entity => entity.RefreshTokenId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId);

        builder.Property(entity => entity.UserId)
            .IsRequired();

        builder.Property(entity => entity.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entity => entity.ExpiresAt)
            .IsRequired();

        builder.Property(entity => entity.RevokedAt);

        builder.Property(entity => entity.RevokedByIp)
            .HasMaxLength(64);

        builder.Property(entity => entity.CreatedByIp)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(entity => entity.ReplacedByTokenHash)
            .HasMaxLength(128);

        builder.Property(entity => entity.SessionId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(entity => entity.ActiveRole)
            .HasMaxLength(120);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.Property(entity => entity.LastActivityAt)
            .IsRequired();

        builder.HasIndex(entity => entity.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_TokenHash");

        builder.HasIndex(entity => new { entity.UserId, entity.SessionId })
            .HasDatabaseName("IX_RefreshTokens_UserId_SessionId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.UserId })
            .HasDatabaseName("IX_RefreshTokens_RestaurantId_UserId");

        builder.HasIndex(entity => entity.ExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

        builder.HasIndex(entity => entity.RevokedAt)
            .HasDatabaseName("IX_RefreshTokens_RevokedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RefreshTokens_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RefreshTokens_Branches_BranchId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RefreshTokens_Users_UserId");
    }
}
