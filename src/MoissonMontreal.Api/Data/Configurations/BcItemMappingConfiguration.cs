using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the bc_item_mappings table and its links to the catalog_items and users tables.
/// </summary>
public class BcItemMappingConfiguration : IEntityTypeConfiguration<BcItemMapping>
{
    /// <summary>
    /// Name of the unique index on the Business Central code, so a code cannot have two mappings.
    /// </summary>
    public const string BusinessCentralCodeIndex = "ix_bc_item_mappings_business_central_code";

    /// <summary>
    /// Configures the columns, the unique Business Central code and the links to the catalog item and the user who confirmed.
    /// </summary>
    /// <param name="builder">Builder used to configure the mapping entity.</param>
    public void Configure(EntityTypeBuilder<BcItemMapping> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_bc_item_mappings_business_central_code_not_empty",
            "length(business_central_code) > 0"));

        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.BusinessCentralCode).HasMaxLength(CatalogItem.BusinessCentralCodeMaxLength);

        builder.HasIndex(m => m.BusinessCentralCode)
            .IsUnique()
            .HasDatabaseName(BusinessCentralCodeIndex);

        builder.Ignore(m => m.NeedsReview);

        builder.HasOne(m => m.CatalogItem)
            .WithMany()
            .HasForeignKey(m => m.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ConfirmedBy)
            .WithMany()
            .HasForeignKey(m => m.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
