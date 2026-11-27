namespace MoissonMontreal.Core.Receipts;

public class ReceiptNotFoundException(Guid receiptId) : Exception($"Receipt {receiptId} does not exist.")
{
    public Guid ReceiptId { get; } = receiptId;
}
