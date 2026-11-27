using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Api.Data.Configurations;

/// <summary>
/// Database rules of the receipts table and its link to the donors table.
/// </summary>
public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    /// <summary>
    /// Configures the columns, constraint and donor relationship of the receipts table.
    /// </summary>
    /// <param name="builder">Builder used to configure the receipt entity.</param>
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_receipts_total_amount_not_negative", "total_amount >= 0");
            t.HasCheckConstraint("ck_receipts_status_valid", ReceiptStatusColumn.IsValid("status"));
        });

        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.TotalAmount).HasPrecision(12, 2);

        builder.Property(r => r.ReceiptDate).HasDefaultValueSql("(now() AT TIME ZONE 'America/Montreal')::date");

        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(ReceiptStatusColumn.MaxLength)
            .HasDefaultValue(ReceiptStatus.Draft);

        builder.HasOne(r => r.Donor)
            .WithMany()
            .HasForeignKey(r => r.DonorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}