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
    /// Configures the columns and constraint of the catalog_items table.
    /// </summary>
    /// <param name="builder">Builder used to configure the catalog item entity.</param>
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_catalog_items_name_not_empty", "length(name) > 0"));

        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name).HasMaxLength(255);

        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
    }
}
