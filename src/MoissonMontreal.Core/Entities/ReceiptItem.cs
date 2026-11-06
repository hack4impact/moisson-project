namespace MoissonMontreal.Core.Entities;

/// <summary>
/// One food item on a receipt, with the price and quality percentage used to value it.
/// The value of the line is the item's quantity times the unit price times the quality percentage.
/// </summary>
public class ReceiptItem
{
    /// <summary>
    /// Identifier of the receipt this line belongs to.
    /// </summary>
    public Guid ReceiptId { get; set; }

    /// <summary>
    /// Receipt this line belongs to.
    /// </summary>
    public Receipt Receipt { get; set; } = null!;

    /// <summary>
    /// Identifier of the food item valued on this line.
    /// </summary>
    public Guid FoodItemId { get; set; }

    /// <summary>
    /// Food item valued on this line.
    /// </summary>
    public FoodItem FoodItem { get; set; } = null!;

    /// <summary>
    /// Price of one unit of the food item when the receipt was made, in dollars. Must be greater than zero.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Percentage of the price applied for the item's quality grade, from 0 to 100.
    /// </summary>
    public decimal QualityPercent { get; set; }
}