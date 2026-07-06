using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(entity => entity.RoleId);

        builder.Property(entity => entity.RoleId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId);

        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(entity => entity.Description)
            .HasMaxLength(512);

        builder.Property(entity => entity.IsSystemRole)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RestaurantId, entity.Name })
            .IsUnique()
            .HasDatabaseName("UX_Roles_RestaurantId_Name")
            .HasFilter("[RestaurantId] IS NOT NULL");

        builder.HasIndex(entity => entity.Name)
            .IsUnique()
            .HasDatabaseName("UX_Roles_Name_System")
            .HasFilter("[RestaurantId] IS NULL");
    }
}
