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
        builder.ToTable(t => t.HasCheckConstraint("ck_donors_name_not_empty", "length(name) > 0"));

        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Name).HasMaxLength(255);

        builder.Property(d => d.PostalCode).HasMaxLength(20);

        builder.Property(d => d.ContactPerson).HasMaxLength(255);

        builder.Property(d => d.CreatedAt).HasDefaultValueSql("now()");
    }
}
