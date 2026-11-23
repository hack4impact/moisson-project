namespace MoissonMontreal.Core.Receipts;

/// <summary>
/// Thrown when another employee changed the status of the receipt between the moment it was read and the moment it was saved.
/// </summary>
public class ReceiptStatusChangedException(Guid receiptId)
    : Exception($"The status of receipt {receiptId} was changed by someone else in the meantime. Reload the receipt and try again.")
{
    public Guid ReceiptId { get; } = receiptId;
}
