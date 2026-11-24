namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// A Business Central item mapping returned by the API, with the unit and category of its catalog item
/// so a person can check that the match is right. NeedsReview is true when the catalog item is no longer active.
/// </summary>
public record BcItemMappingDto(
    Guid Id,
    string BusinessCentralCode,
    Guid CatalogItemId,
    string CatalogItemName,
    string Unit,
    string Category,
    string? QualityClass,
    bool NeedsReview,
    int ConfirmedByUserId,
    string ConfirmedByName,
    DateTimeOffset ConfirmedAt);
