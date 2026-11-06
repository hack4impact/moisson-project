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
    /// Date and time the receipt was created, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Food items on this receipt, with the price used for each one.
    /// </summary>
    public ICollection<ReceiptItem> Items { get; set; } = [];
}