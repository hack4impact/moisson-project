namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// A catalog item returned by the API.
/// </summary>
public record CatalogItemDto(
    Guid Id,
    string Name,
    string Category,
    string BusinessCentralCode,
    string Unit,
    string QualityClass,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
