using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules and initial values of the quality_classes table.
/// </summary>
public class QualityClassConfiguration : IEntityTypeConfiguration<QualityClass>
{
    /// <summary>
    /// Configures the key, exact percentages, constraints and the three initial classes.
    /// The initial values follow the PRD and are still to be confirmed with Moisson Montreal.
    /// </summary>
    /// <param name="builder">Builder used to configure the quality class entity.</param>
    public void Configure(EntityTypeBuilder<QualityClass> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_quality_classes_code_not_empty", "length(code) > 0");
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

        builder.HasData(
            new QualityClass { Code = "A", Name = "Grocery quality", Percentage = 100m, MinPercentage = 100m, MaxPercentage = 100m },
            new QualityClass { Code = "B", Name = "Imperfect", Percentage = 50m, MinPercentage = 50m, MaxPercentage = 75m },
            new QualityClass { Code = "C", Name = "Near expiry", Percentage = 0m, MinPercentage = 0m, MaxPercentage = 0m });
    }
}
