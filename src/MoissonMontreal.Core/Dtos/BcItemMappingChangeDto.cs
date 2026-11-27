namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// One change in the history of a Business Central item mapping. Action is Created, Updated or Deleted.
/// OldCatalogItemId is null for a creation and NewCatalogItemId is null for a deletion.
/// </summary>
public record BcItemMappingChangeDto(
    Guid Id,
    string BusinessCentralCode,
    string Action,
    Guid? OldCatalogItemId,
    Guid? NewCatalogItemId,
    int ChangedByUserId,
    string ChangedByName,
    DateTimeOffset ChangedAt);
