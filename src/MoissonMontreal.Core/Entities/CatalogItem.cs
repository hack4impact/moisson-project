namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Type of food recognized by the app, such as bread or apples, which the price grid gives a price to.
/// Unlike a <see cref="FoodItem"/>, which is one donation, a catalog item is shared by every donation of that food.
/// </summary>
public class CatalogItem
{
    /// <summary>
    /// Identifier of the catalog item, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the food, such as Bread. Cannot be empty.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Date and time the catalog item was created, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Every price this item has had, one per effective date.
    /// </summary>
    public ICollection<Price> Prices { get; set; } = [];
}
