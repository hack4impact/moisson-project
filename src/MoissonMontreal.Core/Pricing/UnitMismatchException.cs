namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Thrown when a donated food item is counted in another unit than the price of its catalog item, such as kg and case.
/// The two cannot be multiplied, and no conversion is guessed until the price unit is decided (issue 28).
/// </summary>
/// <param name="foodItemId">Identifier of the donated food item.</param>
/// <param name="donationUnit">Unit of the donated quantity.</param>
/// <param name="catalogUnit">Unit of the catalog item, which is the unit of its price.</param>
public class UnitMismatchException(Guid foodItemId, string donationUnit, string catalogUnit)
    : Exception($"Food item {foodItemId} is counted in {donationUnit}, but its price is per {catalogUnit}.")
{
    /// <summary>
    /// Identifier of the donated food item.
    /// </summary>
    public Guid FoodItemId { get; } = foodItemId;

    /// <summary>
    /// Unit of the donated quantity.
    /// </summary>
    public string DonationUnit { get; } = donationUnit;

    /// <summary>
    /// Unit of the catalog item, which is the unit of its price.
    /// </summary>
    public string CatalogUnit { get; } = catalogUnit;
}
