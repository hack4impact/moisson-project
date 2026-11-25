namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when a person confirms a new Business Central item mapping.
/// </summary>
public record BcItemMappingCreateRequest(
    string? BusinessCentralCode,
    Guid? CatalogItemId);
