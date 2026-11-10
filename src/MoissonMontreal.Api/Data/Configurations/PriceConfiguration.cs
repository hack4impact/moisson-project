using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the prices table and its link to the catalog_items table.
/// </summary>
public class PriceConfiguration : IEntityTypeConfiguration<Price>
{
    /// <summary>
    /// Configures the columns, constraint, unique effective date and catalog item relationship of the prices table.
    /// </summary>
    /// <param name="builder">Builder used to configure the price entity.</param>
    public void Configure(EntityTypeBuilder<Price> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_prices_unit_price_positive", "unit_price > 0"));

        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.UnitPrice).HasPrecision(12, 4);

        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(p => new { p.CatalogItemId, p.EffectiveDate }).IsUnique();

        builder.HasOne(p => p.CatalogItem)
            .WithMany(c => c.Prices)
            .HasForeignKey(p => p.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
