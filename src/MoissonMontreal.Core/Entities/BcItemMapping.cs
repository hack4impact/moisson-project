namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Remembered match between a Business Central item code and the catalog item whose price values it.
/// A mapping is only created after a person confirms it, and it is reused automatically on the next imports.
/// </summary>
public class BcItemMapping
{
    /// <summary>
    /// Identifier of the mapping, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Item code found in the Business Central donation export. A code can only have one mapping.
    /// </summary>
    public string BusinessCentralCode { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the catalog item used to value this Business Central item.
    /// </summary>
    public Guid CatalogItemId { get; set; }

    /// <summary>
    /// Catalog item used to value this Business Central item. When it is inactive, the mapping must be reviewed.
    /// </summary>
    public CatalogItem CatalogItem { get; set; } = null!;

    /// <summary>
    /// Identifier of the user who last confirmed the mapping.
    /// </summary>
    public int ConfirmedByUserId { get; set; }

    /// <summary>
    /// User who last confirmed the mapping.
    /// </summary>
    public User ConfirmedBy { get; set; } = null!;

    /// <summary>
    /// Date and time the mapping was last confirmed.
    /// </summary>
    public DateTimeOffset ConfirmedAt { get; set; }

    /// <summary>
    /// True when the catalog item is no longer active, so a person must pick another one.
    /// </summary>
    public bool NeedsReview => !CatalogItem.IsActive;
}
