namespace MoissonMontreal.Core.Receipts;

public class InvalidReceiptTransitionException(
    ReceiptStatus status,
    ReceiptAction action,
    IReadOnlyList<ReceiptAction> allowedActions)
    : Exception(BuildMessage(status, action, allowedActions))
{
    public ReceiptStatus Status { get; } = status;

    public ReceiptAction Action { get; } = action;

    public IReadOnlyList<ReceiptAction> AllowedActions { get; } = allowedActions;

    private static string BuildMessage(ReceiptStatus status, ReceiptAction action, IReadOnlyList<ReceiptAction> allowedActions) =>
        allowedActions.Count == 0
            ? $"{action} is not allowed on a receipt in status {status}: no action is possible in this status."
            : $"{action} is not allowed on a receipt in status {status}. Allowed actions: {string.Join(", ", allowedActions)}.";
}
