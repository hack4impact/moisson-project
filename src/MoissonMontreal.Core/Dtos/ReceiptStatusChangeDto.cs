using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Core.Dtos;

public record ReceiptStatusChangeDto(
    Guid ReceiptId,
    ReceiptStatus FromStatus,
    ReceiptStatus ToStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? Reason,
    IReadOnlyList<ReceiptAction> AllowedActions);
