namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Price of one unit of a catalog item from its effective date until the next price of that item starts.
/// A price is never changed: a new price is added as a new row, so past donations keep the price that applied at the time.
/// </summary>
public class Price
{
    /// <summary>
    /// Identifier of the price, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the catalog item this price belongs to.
    /// </summary>
    public Guid CatalogItemId { get; set; }

    /// <summary>
    /// Catalog item this price belongs to.
    /// </summary>
    public CatalogItem CatalogItem { get; set; } = null!;

    /// <summary>
    /// Price of one unit, in dollars, with up to four decimal places. Must be greater than zero.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// First day the price applies. Only one price per catalog item can start on a given day.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Date and time the price was entered, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
