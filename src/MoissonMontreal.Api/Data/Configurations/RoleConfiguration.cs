using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the roles table and the four roles inserted by the migration.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the columns, unique indexes and seed data of the roles table.
    /// </summary>
    /// <param name="builder">Builder used to configure the role entity.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        builder.HasData(
            new Role { Id = 1, Code = "Supply", Name = "Supply" },
            new Role { Id = 2, Code = "SupplyDirector", Name = "Supply Director" },
            new Role { Id = 3, Code = "Finance", Name = "Finance" },
            new Role { Id = 4, Code = "Admin", Name = "Admin" });
    }
}