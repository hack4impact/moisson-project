using MoissonMontreal.Core.Authorization;

namespace MoissonMontreal.Core.Receipts;

/// <summary>
/// The only status changes a receipt can go through. Any change not listed in <see cref="Transitions"/> is refused.
/// The diagram in docs/database/receipt-statuses.md must match this list.
/// </summary>
public static class ReceiptStateMachine
{
    private static readonly ReceiptStatus[] CancellableStatuses =
    [
        ReceiptStatus.Draft,
        ReceiptStatus.PendingSupplyApproval,
        ReceiptStatus.PendingFinanceApproval,
        ReceiptStatus.Approved,
        ReceiptStatus.Issued,
    ];

    public static IReadOnlyList<ReceiptTransition> Transitions { get; } =
    [
        new(ReceiptStatus.Draft, ReceiptAction.Submit, ReceiptStatus.PendingSupplyApproval, Permissions.ManageReceipts),
        new(ReceiptStatus.PendingSupplyApproval, ReceiptAction.ApproveLevel1, ReceiptStatus.PendingFinanceApproval, Permissions.ApproveReceiptsLevel1, ForbiddenToSubmitter: true),
        new(ReceiptStatus.PendingSupplyApproval, ReceiptAction.RefuseLevel1, ReceiptStatus.Draft, Permissions.ApproveReceiptsLevel1, RequiresReason: true),
        new(ReceiptStatus.PendingFinanceApproval, ReceiptAction.ApproveLevel2, ReceiptStatus.Approved, Permissions.ApproveReceiptsLevel2, ForbiddenToSubmitter: true),
        new(ReceiptStatus.PendingFinanceApproval, ReceiptAction.RefuseLevel2, ReceiptStatus.Draft, Permissions.ApproveReceiptsLevel2, RequiresReason: true),
        new(ReceiptStatus.Approved, ReceiptAction.Issue, ReceiptStatus.Issued, Permissions.ManageReceipts),
        .. CancellableStatuses.Select(status =>
            new ReceiptTransition(status, ReceiptAction.Cancel, ReceiptStatus.Cancelled, Permissions.ManageReceipts, RequiresReason: true)),
    ];

    public static IReadOnlyList<ReceiptAction> AllowedActions(ReceiptStatus status) =>
        [.. Transitions.Where(t => t.From == status).Select(t => t.Action)];

    /// <summary>
    /// Returns the transition for an action on a receipt in a given status.
    /// </summary>
    /// <exception cref="InvalidReceiptTransitionException">The action is not allowed from that status.</exception>
    public static ReceiptTransition GetTransition(ReceiptStatus status, ReceiptAction action) =>
        Transitions.SingleOrDefault(t => t.From == status && t.Action == action)
        ?? throw new InvalidReceiptTransitionException(status, action, AllowedActions(status));
}
