namespace MoissonMontreal.Core.Receipts;

public class SubmitterCannotApproveException(Guid receiptId, ReceiptAction action)
    : Exception($"{action} is not allowed on receipt {receiptId}: the employee who submitted a receipt cannot approve it.")
{
    public Guid ReceiptId { get; } = receiptId;

    public ReceiptAction Action { get; } = action;
}
