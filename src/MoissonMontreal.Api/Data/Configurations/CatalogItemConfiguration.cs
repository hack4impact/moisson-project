using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the catalog_items table.
/// </summary>
public class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    /// <summary>
    /// Name of the unique index on the Business Central code.
    /// </summary>
    public const string BusinessCentralCodeIndex = "ix_catalog_items_business_central_code";

    /// <summary>
    /// Name of the foreign key from the catalog item to its quality class.
    /// </summary>
    public const string QualityClassForeignKey = "fk_catalog_items_quality_classes_quality_class";

    /// <summary>
    /// Configures the columns, the unique Business Central code and the link to the quality class.
    /// </summary>
    /// <param name="builder">Builder used to configure the catalog item entity.</param>
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_catalog_items_name_not_empty", "length(name) > 0");
            t.HasCheckConstraint("ck_catalog_items_category_not_empty", "length(category) > 0");
            t.HasCheckConstraint("ck_catalog_items_business_central_code_not_empty", "length(business_central_code) > 0");
            t.HasCheckConstraint("ck_catalog_items_unit_not_empty", "length(unit) > 0");
        });

        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name).HasMaxLength(CatalogItem.NameMaxLength);

        builder.Property(c => c.Category).HasMaxLength(CatalogItem.CategoryMaxLength);

        builder.Property(c => c.BusinessCentralCode).HasMaxLength(CatalogItem.BusinessCentralCodeMaxLength);

        builder.HasIndex(c => c.BusinessCentralCode)
            .IsUnique()
            .HasDatabaseName(BusinessCentralCodeIndex);

        builder.Property(c => c.Unit).HasMaxLength(CatalogItem.UnitMaxLength);

        builder.Property(c => c.QualityClassCode)
            .HasColumnName("quality_class")
            .HasMaxLength(1);

        builder.HasIndex(c => c.QualityClassCode)
            .HasDatabaseName("ix_catalog_items_quality_class");

        builder.HasOne(c => c.QualityClass)
            .WithMany()
            .HasForeignKey(c => c.QualityClassCode)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(QualityClassForeignKey);

        builder.Property(c => c.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
    }
}
