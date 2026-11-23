using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Column settings for the donors table.
/// </summary>
public class DonorConfiguration : IEntityTypeConfiguration<Donor>
{
    /// <summary>
    /// Name of the unique index on the Business Central code.
    /// </summary>
    public const string BusinessCentralCodeIndex = "ix_donors_business_central_code";

    /// <summary>
    /// Configures the columns, the allowed types and the unique Business Central code.
    /// </summary>
    /// <param name="builder">Builder used to configure the donor entity.</param>
    public void Configure(EntityTypeBuilder<Donor> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_donors_name_not_empty", "length(name) > 0");
            t.HasCheckConstraint("ck_donors_type", $"type IN ('{DonorTypes.Individual}', '{DonorTypes.Organization}')");
            t.HasCheckConstraint("ck_donors_business_central_code_not_empty", "length(business_central_code) > 0");
        });

        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Name).HasMaxLength(Donor.NameMaxLength);

        builder.Property(d => d.Type).HasMaxLength(20);

        builder.Property(d => d.City).HasMaxLength(Donor.CityMaxLength);

        builder.Property(d => d.Province).HasMaxLength(Donor.ProvinceMaxLength);

        builder.Property(d => d.PostalCode).HasMaxLength(Donor.PostalCodeMaxLength);

        builder.Property(d => d.ContactPerson).HasMaxLength(Donor.ContactPersonMaxLength);

        builder.Property(d => d.Email).HasMaxLength(Donor.EmailMaxLength);

        builder.Property(d => d.Phone).HasMaxLength(Donor.PhoneMaxLength);

        builder.Property(d => d.BusinessCentralCode).HasMaxLength(Donor.BusinessCentralCodeMaxLength);

        builder.HasIndex(d => d.BusinessCentralCode)
            .IsUnique()
            .HasDatabaseName(BusinessCentralCodeIndex);

        builder.Property(d => d.CreatedAt).HasDefaultValueSql("now()");
    }
}
