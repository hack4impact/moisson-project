using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the users table and its link to the roles table.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Configures the columns, constraints, unique indexes and role relationship of the users table.
    /// </summary>
    /// <param name="builder">Builder used to configure the user entity.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_users_email_lowercase", "email = lower(email)"));

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasIndex(u => u.EntraObjectId).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();

        builder.Property(u => u.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}