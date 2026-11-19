namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Thrown when the value of a donated food item is asked for an item that does not exist.
/// </summary>
/// <param name="foodItemId">Identifier that matches no donated food item.</param>
public class FoodItemNotFoundException(Guid foodItemId)
    : Exception($"Food item {foodItemId} does not exist.")
{
    /// <summary>
    /// Identifier that matches no donated food item.
    /// </summary>
    public Guid FoodItemId { get; } = foodItemId;
}
