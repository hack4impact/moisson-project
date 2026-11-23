using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Api.Data.Configurations;

public class ReceiptNumberCounterConfiguration : IEntityTypeConfiguration<ReceiptNumberCounter>
{
    public void Configure(EntityTypeBuilder<ReceiptNumberCounter> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_receipt_number_counters_last_number_in_range",
            $"last_number BETWEEN 0 AND {ReceiptNumber.LastSequence}"));

        builder.HasKey(c => c.Series);

        builder.Property(c => c.Series).HasMaxLength(10);

        builder.HasData(new ReceiptNumberCounter { Series = ReceiptNumber.Series, LastNumber = 0 });
    }
}
