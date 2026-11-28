using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Api.Data.Configurations;

public class ReceiptStatusChangeConfiguration : IEntityTypeConfiguration<ReceiptStatusChange>
{
    public void Configure(EntityTypeBuilder<ReceiptStatusChange> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_receipt_status_changes_from_status_valid", ReceiptStatusColumn.IsValid("from_status"));
            t.HasCheckConstraint("ck_receipt_status_changes_to_status_valid", ReceiptStatusColumn.IsValid("to_status"));
            t.HasCheckConstraint("ck_receipt_status_changes_status_differs", "from_status <> to_status");
            t.HasCheckConstraint("ck_receipt_status_changes_reason_not_empty", "length(reason) > 0");
            t.HasCheckConstraint(
                "ck_receipt_status_changes_reason_required",
                $"to_status NOT IN ('{nameof(ReceiptStatus.Draft)}', '{nameof(ReceiptStatus.Cancelled)}') OR reason IS NOT NULL");
        });

        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(ReceiptStatusColumn.MaxLength);

        builder.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(ReceiptStatusColumn.MaxLength);

        builder.Property(c => c.Reason).HasMaxLength(ReceiptStatusChange.ReasonMaxLength);

        builder.Property(c => c.ChangedAt).HasDefaultValueSql("now()");

        builder.HasIndex(c => new { c.ReceiptId, c.ChangedAt });

        builder.HasOne(c => c.Receipt)
            .WithMany(r => r.StatusChanges)
            .HasForeignKey(c => c.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ChangedBy)
            .WithMany()
            .HasForeignKey(c => c.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
