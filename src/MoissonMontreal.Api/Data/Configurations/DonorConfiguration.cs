using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Column settings for the donors table.
/// </summary>
public class DonorConfiguration : IEntityTypeConfiguration<Donor>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Donor> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(255);

        builder.Property(d => d.PostalCode).HasMaxLength(20);

        builder.Property(d => d.ContactPerson).HasMaxLength(255);

        builder.Property(d => d.CreatedAt)
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
    }
}
