namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// One price of a catalog item, as returned by the API.
/// </summary>
/// <param name="Id">Identifier of the price.</param>
/// <param name="UnitPrice">Price of one unit, in dollars, with up to four decimal places.</param>
/// <param name="EffectiveDate">First day the price applies.</param>
/// <param name="CreatedAt">Date and time the price was entered, in UTC.</param>
public record PriceDto(Guid Id, decimal UnitPrice, DateOnly EffectiveDate, DateTimeOffset CreatedAt);

/// <summary>
/// Every price a catalog item has had, oldest effective date first.
/// </summary>
/// <param name="CatalogItemId">Identifier of the catalog item.</param>
/// <param name="CatalogItemName">Name of the catalog item, such as Bread.</param>
/// <param name="Prices">Prices of the item, sorted by effective date. Empty when the item has no price yet.</param>
public record PriceHistoryDto(Guid CatalogItemId, string CatalogItemName, IReadOnlyList<PriceDto> Prices);