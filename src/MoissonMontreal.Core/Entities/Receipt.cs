using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Tax receipt given to a donor for the food items they donated.
/// </summary>
public class Receipt
{
    /// <summary>
    /// Identifier of the receipt, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the donor who receives this receipt.
    /// </summary>
    public Guid DonorId { get; set; }

    /// <summary>
    /// Donor who receives this receipt.
    /// </summary>
    public Donor Donor { get; set; } = null!;

    /// <summary>
    /// Total value of the receipt in dollars, with two decimal places. Cannot be negative.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Date of the receipt in Montreal time, set by the database. Used for the tax year.
    /// </summary>
    public DateOnly ReceiptDate { get; set; }

    /// <summary>
    /// Date and time the receipt was created, set by the database. Stored and read back in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Food items on this receipt, with the price used for each one.
    /// </summary>
    public ICollection<ReceiptItem> Items { get; set; } = [];

    /// <summary>
    /// Where the receipt is in the approval process. Draft for a new receipt.
    /// Change it only through <see cref="ReceiptStateMachine"/>, so every change is allowed and recorded.
    /// </summary>
    public ReceiptStatus Status { get; set; }

    /// <summary>
    /// Unique number of the receipt, such as DEN_000001, given when the receipt is issued and kept if it is cancelled.
    /// Empty until then.
    /// </summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>
    /// Every status change of this receipt, with who made it and when.
    /// </summary>
    public ICollection<ReceiptStatusChange> StatusChanges { get; set; } = [];
}