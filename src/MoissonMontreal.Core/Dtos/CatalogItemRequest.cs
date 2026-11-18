namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when a catalog item is created or edited.
/// </summary>
public record CatalogItemRequest(
    string? Name,
    string? Category,
    string? BusinessCentralCode,
    string? Unit,
    string? QualityClass);
