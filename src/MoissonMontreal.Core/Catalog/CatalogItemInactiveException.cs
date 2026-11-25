namespace MoissonMontreal.Core.Catalog;

/// <summary>
/// Thrown when a mapping is pointed to a catalog item that was removed from the price grid.
/// </summary>
/// <param name="catalogItemId">Identifier of the inactive catalog item.</param>
public class CatalogItemInactiveException(Guid catalogItemId)
    : Exception($"Catalog item {catalogItemId} is no longer active.")
{
    /// <summary>
    /// Identifier of the inactive catalog item.
    /// </summary>
    public Guid CatalogItemId { get; } = catalogItemId;
}
