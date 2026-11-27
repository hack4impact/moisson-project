namespace MoissonMontreal.Core.Entities;

/// <summary>
/// One change made to a Business Central item mapping, kept for history.
/// The row stores the code instead of a link to the mapping, so the history stays after a mapping is deleted.
/// </summary>
public class BcItemMappingChange
{
    /// <summary>
    /// Identifier of the change, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Business Central item code whose mapping changed.
    /// </summary>
    public string BusinessCentralCode { get; set; } = string.Empty;

    /// <summary>
    /// Kind of change: created, updated or deleted.
    /// </summary>
    public BcItemMappingAction Action { get; set; }

    /// <summary>
    /// Catalog item the code pointed to before the change, or null when the mapping was created.
    /// </summary>
    public Guid? OldCatalogItemId { get; set; }

    /// <summary>
    /// Catalog item the code points to after the change, or null when the mapping was deleted.
    /// </summary>
    public Guid? NewCatalogItemId { get; set; }

    /// <summary>
    /// Identifier of the user who made the change.
    /// </summary>
    public int ChangedByUserId { get; set; }

    /// <summary>
    /// User who made the change.
    /// </summary>
    public User ChangedBy { get; set; } = null!;

    /// <summary>
    /// Date and time of the change, set by the database.
    /// </summary>
    public DateTimeOffset ChangedAt { get; set; }
}
