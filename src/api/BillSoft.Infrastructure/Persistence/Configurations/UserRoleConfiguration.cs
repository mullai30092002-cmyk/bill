using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(entity => entity.UserRoleId);

        builder.Property(entity => entity.UserRoleId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.AssignedAt)
            .IsRequired();

        builder.HasIndex(entity => new { entity.UserId, entity.RoleId })
            .IsUnique()
            .HasDatabaseName("UX_UserRoles_UserId_RoleId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserRoles_Users_UserId");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserRoles_Roles_RoleId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserRoles_Users_AssignedByUserId");
    }
}
