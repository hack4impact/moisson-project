using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the quality_classes table.
/// </summary>
public class QualityClassConfiguration : IEntityTypeConfiguration<QualityClass>
{
    /// <summary>
    /// Configures the key, exact percentages and constraints.
    /// The three initial classes are inserted by the CreateQualityClasses migration only, not with HasData,
    /// so a later migration never overwrites the values the admin changes in the table.
    /// </summary>
    /// <param name="builder">Builder used to configure the quality class entity.</param>
    public void Configure(EntityTypeBuilder<QualityClass> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_quality_classes_code_format", "code ~ '^[A-Z]$'");
            t.HasCheckConstraint("ck_quality_classes_name_not_empty", "length(name) > 0");
            t.HasCheckConstraint("ck_quality_classes_range_valid", "min_percentage >= 0 AND min_percentage <= max_percentage AND max_percentage <= 100");
            t.HasCheckConstraint("ck_quality_classes_percentage_in_range", "percentage BETWEEN min_percentage AND max_percentage");
        });

        builder.HasKey(q => q.Code);

        builder.Property(q => q.Code).HasMaxLength(1);

        builder.Property(q => q.Name).HasMaxLength(100);
        builder.HasIndex(q => q.Name).IsUnique();

        builder.Property(q => q.Percentage).HasPrecision(5, 2);
        builder.Property(q => q.MinPercentage).HasPrecision(5, 2);
        builder.Property(q => q.MaxPercentage).HasPrecision(5, 2);
    }
}
