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
    /// Configures the keys, copied values, constraints and relationships of the receipt_items table.
    /// Both links include donor_id, so a receipt can only list food items given by its own donor.
    /// </summary>
    /// <param name="builder">Builder used to configure the receipt item entity.</param>
    public void Configure(EntityTypeBuilder<ReceiptItem> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_receipt_items_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_receipt_items_unit_not_empty", "length(unit) > 0");
            t.HasCheckConstraint("ck_receipt_items_unit_price_positive", "unit_price > 0");
            t.HasCheckConstraint("ck_receipt_items_quality_percent_range", "quality_percent BETWEEN 0 AND 100");
        });

        builder.HasKey(i => new { i.ReceiptId, i.FoodItemId });

        builder.HasIndex(i => i.FoodItemId).IsUnique();

        builder.Property(i => i.Quantity).HasPrecision(12, 2);

        builder.Property(i => i.Unit).HasMaxLength(20);

        builder.Property(i => i.UnitPrice).HasPrecision(12, 4);

        builder.Property(i => i.QualityPercent).HasPrecision(5, 2);

        builder.HasOne(i => i.Receipt)
            .WithMany(r => r.Items)
            .HasForeignKey(i => new { i.ReceiptId, i.DonorId })
            .HasPrincipalKey(r => new { r.Id, r.DonorId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.FoodItem)
            .WithMany()
            .HasForeignKey(i => new { i.FoodItemId, i.DonorId })
            .HasPrincipalKey(f => new { f.Id, f.DonorId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}