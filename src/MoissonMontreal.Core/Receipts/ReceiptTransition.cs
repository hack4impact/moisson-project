namespace MoissonMontreal.Core.Receipts;

/// <summary>
/// One allowed status change of a receipt.
/// </summary>
/// <param name="Permission">Permission from <see cref="Authorization.Permissions"/> needed to perform the action.</param>
/// <param name="RequiresReason">True when the employee must explain the change, such as a refusal or a cancellation.</param>
/// <param name="ForbiddenToSubmitter">True when the employee who submitted the receipt cannot perform the action, so nobody approves their own receipt.</param>
public sealed record ReceiptTransition(
    ReceiptStatus From,
    ReceiptAction Action,
    ReceiptStatus To,
    string Permission,
    bool RequiresReason = false,
    bool ForbiddenToSubmitter = false);
