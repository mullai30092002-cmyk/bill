using BillSoft.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(entity => entity.PermissionId);

        builder.Property(entity => entity.PermissionId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.Code)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(entity => entity.Description)
            .HasMaxLength(512);

        builder.Property(entity => entity.Module)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(entity => entity.Code)
            .IsUnique()
            .HasDatabaseName("UX_Permissions_Code");
    }
}
