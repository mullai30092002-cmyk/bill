using BillSoft.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(entity => entity.RolePermissionId);

        builder.Property(entity => entity.RolePermissionId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RoleId, entity.PermissionId })
            .IsUnique()
            .HasDatabaseName("UX_RolePermissions_RoleId_PermissionId");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RolePermissions_Roles_RoleId");

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(entity => entity.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RolePermissions_Permissions_PermissionId");
    }
}
