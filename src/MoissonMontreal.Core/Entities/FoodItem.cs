namespace MoissonMontreal.Core.Entities;

/// <summary>
/// One food donation from a donor, including the date it was given and its quality grade.
/// </summary>
public class FoodItem
{
    /// <summary>
    /// Identifier of the food item, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the donor who gave this item.
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
    /// Amount donated, with up to two decimal places. Must be greater than zero.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit of the quantity, as free text for now (for example kg).
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
    /// Identifier of the catalog item this donation is, which gives its price.
    /// Empty until the donation is matched to the catalog.
    /// </summary>
    public Guid? CatalogItemId { get; set; }

    /// <summary>
    /// Catalog item this donation is, or null until the donation is matched to the catalog.
    /// </summary>
    public CatalogItem? CatalogItem { get; set; }

    /// <summary>
    /// Date and time the item was added, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
