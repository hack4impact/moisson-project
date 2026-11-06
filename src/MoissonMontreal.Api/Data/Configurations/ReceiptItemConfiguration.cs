using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the receipt_items table and its links to the receipts and food_items tables.
/// </summary>
public class ReceiptItemConfiguration : IEntityTypeConfiguration<ReceiptItem>
{
    /// <summary>
    /// Configures the key, price columns, constraints and relationships of the receipt_items table.
    /// </summary>
    /// <param name="builder">Builder used to configure the receipt item entity.</param>
    public void Configure(EntityTypeBuilder<ReceiptItem> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_receipt_items_unit_price_positive", "unit_price > 0");
            t.HasCheckConstraint("ck_receipt_items_quality_percent_range", "quality_percent BETWEEN 0 AND 100");
        });

        builder.HasKey(i => new { i.ReceiptId, i.FoodItemId });

        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);

        builder.Property(i => i.QualityPercent).HasPrecision(5, 2);

        builder.HasOne(i => i.Receipt)
            .WithMany(r => r.Items)
            .HasForeignKey(i => i.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.FoodItem)
            .WithMany()
            .HasForeignKey(i => i.FoodItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}