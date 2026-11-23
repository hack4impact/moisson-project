using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Authorization;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Api.Receipts;

public class ReceiptStatusService(MoissonDbContext db, UserLookupService users)
{
    /// <summary>
    /// Applies an action to a receipt: checks it against <see cref="ReceiptStateMachine"/>, then changes the status and
    /// records who made the change in one transaction. The status is only changed if it is still the one that was read,
    /// so two employees acting at the same time cannot both succeed.
    /// </summary>
    /// <exception cref="ReceiptNotFoundException">The receipt does not exist.</exception>
    /// <exception cref="InvalidReceiptTransitionException">The action is not allowed from the current status.</exception>
    /// <exception cref="InvalidReceiptReasonException">The reason is missing, empty or too long.</exception>
    /// <exception cref="SubmitterCannotApproveException">The employee submitted the receipt and tries to approve it.</exception>
    /// <exception cref="ReceiptStatusChangedException">Someone else changed the status in the meantime.</exception>
    public async Task<ReceiptStatusChange> ApplyAsync(
        Guid receiptId,
        ReceiptAction action,
        string? reason,
        ClaimsPrincipal employee,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindActiveUserAsync(employee, cancellationToken)
            ?? throw new InvalidOperationException("Only an employee with an active account can change the status of a receipt.");

        var status = await db.Receipts
            .Where(r => r.Id == receiptId)
            .Select(r => (ReceiptStatus?)r.Status)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ReceiptNotFoundException(receiptId);

        var transition = ReceiptStateMachine.GetTransition(status, action);
        var savedReason = ReadReason(transition, reason);

        if (transition.ForbiddenToSubmitter && await FindSubmitterIdAsync(receiptId, cancellationToken) == user.Id)
        {
            throw new SubmitterCannotApproveException(receiptId, action);
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var updated = await db.Receipts
            .Where(r => r.Id == receiptId && r.Status == transition.From)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, transition.To), cancellationToken);
        if (updated == 0)
        {
            throw new ReceiptStatusChangedException(receiptId);
        }

        var change = new ReceiptStatusChange
        {
            ReceiptId = receiptId,
            FromStatus = transition.From,
            ToStatus = transition.To,
            ChangedBy = user,
            Reason = savedReason,
        };
        db.ReceiptStatusChanges.Add(change);
        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return change;
    }

    private static string? ReadReason(ReceiptTransition transition, string? reason)
    {
        if (!transition.RequiresReason)
        {
            return null;
        }

        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidReceiptReasonException($"A reason is required for {transition.Action}.");
        }

        if (trimmed.Length > ReceiptStatusChange.ReasonMaxLength)
        {
            throw new InvalidReceiptReasonException($"The reason must be {ReceiptStatusChange.ReasonMaxLength} characters or fewer.");
        }

        return trimmed;
    }

    private Task<int?> FindSubmitterIdAsync(Guid receiptId, CancellationToken cancellationToken) =>
        db.ReceiptStatusChanges
            .Where(c => c.ReceiptId == receiptId && c.ToStatus == ReceiptStatus.PendingSupplyApproval)
            .OrderByDescending(c => c.ChangedAt)
            .Select(c => (int?)c.ChangedByUserId)
            .FirstOrDefaultAsync(cancellationToken);
}
