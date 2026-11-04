namespace MoissonMontreal.Core.Entities;

/// <summary>
/// One food donation from a donor, including the date it was given and its quality grade.
/// </summary>
public class FoodItem
{
    /// <summary>
    /// Identifier of the food item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Donor who gave this item.
    /// </summary>
    public Guid DonorId { get; set; }

    /// <summary>
    /// Donor who gave this item.
    /// </summary>
    public Donor Donor { get; set; } = null!;

    /// <summary>
    /// Name of the donated food.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Amount donated, with up to two decimal places.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit of the quantity, such as kg.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Calendar date of the donation.
    /// </summary>
    public DateOnly DonationDate { get; set; }

    /// <summary>
    /// Quality grade: A, B, or C.
    /// </summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>
    /// When the item was recorded. The database sets this when it is left empty.
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
