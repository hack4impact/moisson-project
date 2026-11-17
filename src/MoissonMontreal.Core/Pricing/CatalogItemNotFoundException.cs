namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Thrown when a price or a price history is asked for a catalog item that does not exist.
/// </summary>
/// <param name="catalogItemId">Identifier that matches no catalog item.</param>
public class CatalogItemNotFoundException(Guid catalogItemId)
    : Exception($"Catalog item {catalogItemId} does not exist.")
{
    /// <summary>
    /// Identifier that matches no catalog item.
    /// </summary>
    public Guid CatalogItemId { get; } = catalogItemId;
}