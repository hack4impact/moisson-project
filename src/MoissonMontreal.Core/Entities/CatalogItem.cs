namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Type of food recognized by the app, such as bread or apples, which the price grid gives a price to.
/// Unlike a <see cref="FoodItem"/>, which is one donation, a catalog item is shared by every donation of that food.
/// </summary>
public class CatalogItem
{
    /// <summary>
    /// Longest name stored for a catalog item.
    /// </summary>
    public const int NameMaxLength = 255;

    /// <summary>
    /// Longest category stored for a catalog item.
    /// </summary>
    public const int CategoryMaxLength = 100;

    /// <summary>
    /// Longest Business Central code stored for a catalog item.
    /// </summary>
    public const int BusinessCentralCodeMaxLength = 50;

    /// <summary>
    /// Longest unit stored for a catalog item. The unit is free text until the price-per-unit decision in issue 28.
    /// </summary>
    public const int UnitMaxLength = 20;

    /// <summary>
    /// Identifier of the catalog item, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the food, such as Bread. Cannot be empty.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Group the food belongs to, such as Bakery. Cannot be empty.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Code of this food in Business Central. Two catalog items cannot share a code.
    /// </summary>
    public string BusinessCentralCode { get; set; } = string.Empty;

    /// <summary>
    /// Unit used to count this food, as free text for now (for example kg or caisse).
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Code of the quality class of this food, or null until someone picks it, as for items imported from Business Central.
    /// When it is set, it must match an existing quality class.
    /// </summary>
    public string? QualityClassCode { get; set; }

    /// <summary>
    /// Quality class of this food, or null until someone picks it. The class lives on the catalog item.
    /// </summary>
    public QualityClass? QualityClass { get; set; }

    /// <summary>
    /// False once the item is removed from the price grid. The row is kept so past donations and prices stay valid.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time the catalog item was created, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Date and time of the last change, or null if the item was never changed.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Every price this item has had, one per effective date.
    /// </summary>
    public ICollection<Price> Prices { get; set; } = [];
}
