using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Core.Entities;

public class ReceiptStatusChange
{
    public const int ReasonMaxLength = 1000;

    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }

    public Receipt Receipt { get; set; } = null!;

    public ReceiptStatus FromStatus { get; set; }

    public ReceiptStatus ToStatus { get; set; }

    public int ChangedByUserId { get; set; }

    public User ChangedBy { get; set; } = null!;

    public DateTimeOffset ChangedAt { get; set; }

    public string? Reason { get; set; }
}
