namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Field sent when a person points an existing mapping to another catalog item. The code comes from the address.
/// </summary>
public record BcItemMappingUpdateRequest(Guid? CatalogItemId);
