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
    /// Name of the unique index on the email, used to recognize a save refused because the email is already used.
    /// </summary>
    public const string EmailIndex = "ix_users_email";

    /// <summary>
    /// Configures the columns, constraints, unique indexes and role relationship of the users table.
    /// </summary>
    /// <param name="builder">Builder used to configure the user entity.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_users_email_lowercase", "email = lower(email)"));

        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(EmailIndex);

        builder.HasIndex(u => u.EntraObjectId).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();

        builder.Property(u => u.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");

        // Mapped to the PostgreSQL system column xmin, which changes on every update, so no column is added.
        builder.Property(u => u.Version).IsRowVersion();

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}