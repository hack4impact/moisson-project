using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Column settings for the food_items table, including the link to the donor.
/// </summary>
public class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(255);

        builder.Property(f => f.Quantity).HasPrecision(12, 2);

        builder.Property(f => f.Unit).HasMaxLength(20);

        builder.Property(f => f.Quality).HasMaxLength(1);

        builder.Property(f => f.CreatedAt)
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.HasOne(f => f.Donor)
            .WithMany(d => d.FoodItems)
            .HasForeignKey(f => f.DonorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
