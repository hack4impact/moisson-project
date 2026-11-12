using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Column settings for the food_items table, including the links to the donor, to the catalog item and to the quality class.
/// </summary>
public class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_food_items_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_food_items_name_not_empty", "length(name) > 0");
            t.HasCheckConstraint("ck_food_items_unit_not_empty", "length(unit) > 0");
        });

        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(f => f.Name).HasMaxLength(255);

        builder.Property(f => f.Quantity).HasPrecision(12, 2);

        builder.Property(f => f.Unit).HasMaxLength(20);

        builder.Property(f => f.Quality).HasMaxLength(1);

        builder.Property(f => f.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(f => f.Donor)
            .WithMany(d => d.FoodItems)
            .HasForeignKey(f => f.DonorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.CatalogItem)
            .WithMany()
            .HasForeignKey(f => f.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.QualityClass)
            .WithMany()
            .HasForeignKey(f => f.Quality)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
