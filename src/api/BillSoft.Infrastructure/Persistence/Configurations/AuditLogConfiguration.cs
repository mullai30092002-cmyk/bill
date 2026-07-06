using BillSoft.Domain.Auditing;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(entity => entity.AuditLogId);

        builder.Property(entity => entity.AuditLogId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.Action)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.EntityType)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.EntityId)
            .HasMaxLength(128);

        builder.Property(entity => entity.OldValueJson);

        builder.Property(entity => entity.NewValueJson);

        builder.Property(entity => entity.Reason)
            .HasMaxLength(500);

        builder.Property(entity => entity.RestaurantNameSnapshot)
            .HasMaxLength(160);

        builder.Property(entity => entity.BranchNameSnapshot)
            .HasMaxLength(160);

        builder.Property(entity => entity.UserNameSnapshot)
            .HasMaxLength(160);

        builder.Property(entity => entity.UserMobileSnapshot)
            .HasMaxLength(32);

        builder.Property(entity => entity.DeviceId)
            .HasMaxLength(128);

        builder.Property(entity => entity.IpAddress)
            .HasMaxLength(64);

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_RestaurantId_BranchId_CreatedAt");

        builder.HasIndex(entity => new { entity.EntityType, entity.EntityId })
            .HasDatabaseName("IX_AuditLogs_EntityType_EntityId");

        builder.HasIndex(entity => new { entity.UserId, entity.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_UserId_CreatedAt");

        builder.HasIndex(entity => new { entity.Action, entity.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_Action_CreatedAt");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AuditLogs_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AuditLogs_Branches_BranchId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AuditLogs_Users_UserId");
    }
}
