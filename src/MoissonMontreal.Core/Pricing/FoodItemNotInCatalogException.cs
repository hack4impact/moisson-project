namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Thrown when a donated food item is not linked to a catalog item yet, so it has no price.
/// The item must first be matched to the catalog; its value is never guessed.
/// </summary>
/// <param name="foodItemId">Identifier of the donated food item that has no catalog item.</param>
public class FoodItemNotInCatalogException(Guid foodItemId)
    : Exception($"Food item {foodItemId} is not matched to a catalog item yet, so it has no price.")
{
    /// <summary>
    /// Identifier of the donated food item that has no catalog item.
    /// </summary>
    public Guid FoodItemId { get; } = foodItemId;
}
