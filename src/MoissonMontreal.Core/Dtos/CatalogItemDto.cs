namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// A catalog item returned by the API. QualityClass is null until someone picks it, as for items imported from Business Central.
/// </summary>
public record CatalogItemDto(
    Guid Id,
    string Name,
    string Category,
    string BusinessCentralCode,
    string Unit,
    string? QualityClass,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
