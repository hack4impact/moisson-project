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
    /// records who made the change in one transaction. The receipt row is locked before its status and its submitter are
    /// read, so an action on the same receipt at the same time waits, then is checked against the new status and submitter.
    /// When the receipt is issued, it also takes the next receipt number in that same transaction.
    /// </summary>
    /// <exception cref="ReceiptNotFoundException">The receipt does not exist.</exception>
    /// <exception cref="InvalidReceiptTransitionException">The action is not allowed from the current status.</exception>
    /// <exception cref="InvalidReceiptReasonException">The reason is missing, empty or too long.</exception>
    /// <exception cref="SubmitterCannotApproveException">The employee submitted the receipt and tries to approve it.</exception>
    public async Task<AppliedReceiptAction> ApplyAsync(
        Guid receiptId,
        ReceiptAction action,
        string? reason,
        ClaimsPrincipal employee,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindActiveUserAsync(employee, cancellationToken)
            ?? throw new InvalidOperationException("Only an employee with an active account can change the status of a receipt.");

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var receipt = (await db.Receipts
            .FromSql($"SELECT * FROM receipts WHERE id = {receiptId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .SingleOrDefault()
            ?? throw new ReceiptNotFoundException(receiptId);

        var transition = ReceiptStateMachine.GetTransition(receipt.Status, action);
        var savedReason = ReadReason(transition, reason);

        if (transition.ForbiddenToSubmitter && await FindSubmitterIdAsync(receiptId, cancellationToken) == user.Id)
        {
            throw new SubmitterCannotApproveException(receiptId, action);
        }

        var newNumber = transition.To == ReceiptStatus.Issued
            ? ReceiptNumber.Format(await TakeNextReceiptSequenceAsync(cancellationToken))
            : null;

        await db.Receipts
            .Where(r => r.Id == receiptId)
            .ExecuteUpdateAsync(
                setters =>
                {
                    setters.SetProperty(r => r.Status, transition.To);
                    if (newNumber is not null)
                    {
                        setters.SetProperty(r => r.ReceiptNumber, newNumber);
                    }
                },
                cancellationToken);

        if (transition.To == ReceiptStatus.Cancelled)
        {
            await db.ReceiptItems
                .Where(i => i.ReceiptId == receiptId)
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.ReceiptCancelled, true), cancellationToken);
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

        return new AppliedReceiptAction(change, newNumber ?? receipt.ReceiptNumber);
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

    /// <summary>
    /// Adds one to the counter of the series and returns the new value. The UPDATE locks the counter row until the
    /// transaction ends: a receipt issued at the same time waits, then gets the next value, and if this transaction is
    /// rolled back, the counter goes back too, so no number is skipped.
    /// </summary>
    private async Task<int> TakeNextReceiptSequenceAsync(CancellationToken cancellationToken)
    {
        var sequences = await db.Database
            .SqlQuery<int>($"""
                UPDATE receipt_number_counters
                SET last_number = last_number + 1
                WHERE series = {ReceiptNumber.Series}
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return sequences.Single();
    }

    private Task<int?> FindSubmitterIdAsync(Guid receiptId, CancellationToken cancellationToken) =>
        db.ReceiptStatusChanges
            .Where(c => c.ReceiptId == receiptId && c.ToStatus == ReceiptStatus.PendingSupplyApproval)
            .OrderByDescending(c => c.ChangedAt)
            .Select(c => (int?)c.ChangedByUserId)
            .FirstOrDefaultAsync(cancellationToken);
}
