using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the bc_item_mapping_changes table, the history of every mapping change.
/// </summary>
public class BcItemMappingChangeConfiguration : IEntityTypeConfiguration<BcItemMappingChange>
{
    /// <summary>
    /// Longest action name stored, such as Created.
    /// </summary>
    private const int ActionMaxLength = 20;

    /// <summary>
    /// Configures the columns, the action stored as text, the index on the code and the links to catalog items and users.
    /// </summary>
    /// <param name="builder">Builder used to configure the mapping change entity.</param>
    public void Configure(EntityTypeBuilder<BcItemMappingChange> builder)
    {
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.BusinessCentralCode).HasMaxLength(CatalogItem.BusinessCentralCodeMaxLength);

        builder.HasIndex(c => c.BusinessCentralCode);

        builder.Property(c => c.Action)
            .HasConversion<string>()
            .HasMaxLength(ActionMaxLength);

        builder.Property(c => c.ChangedAt).HasDefaultValueSql("now()");

        builder.HasOne<CatalogItem>()
            .WithMany()
            .HasForeignKey(c => c.OldCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CatalogItem>()
            .WithMany()
            .HasForeignKey(c => c.NewCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ChangedBy)
            .WithMany()
            .HasForeignKey(c => c.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
