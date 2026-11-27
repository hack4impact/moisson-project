using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the user_account_changes table, the history of every change made to an employee's account.
/// </summary>
public class UserAccountChangeConfiguration : IEntityTypeConfiguration<UserAccountChange>
{
    /// <summary>
    /// Longest action name stored, such as RoleChanged.
    /// </summary>
    private const int ActionMaxLength = 20;

    /// <summary>
    /// Configures the columns, the action stored as text, the index on the account and the links to users and roles.
    /// </summary>
    /// <param name="builder">Builder used to configure the account change entity.</param>
    public void Configure(EntityTypeBuilder<UserAccountChange> builder)
    {
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Action)
            .HasConversion<string>()
            .HasMaxLength(ActionMaxLength);

        builder.Property(c => c.OldEmail).HasMaxLength(User.EmailMaxLength);
        builder.Property(c => c.NewEmail).HasMaxLength(User.EmailMaxLength);

        builder.Property(c => c.OldDisplayName).HasMaxLength(User.DisplayNameMaxLength);
        builder.Property(c => c.NewDisplayName).HasMaxLength(User.DisplayNameMaxLength);

        builder.Property(c => c.ChangedAt).HasDefaultValueSql("now()");

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ChangedBy)
            .WithMany()
            .HasForeignKey(c => c.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(c => c.OldRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(c => c.NewRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
