using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Api.Pricing;

/// <summary>
/// Reads the price grid: the price history of a catalog item and the price in effect on a given day.
/// The donation value calculation relies on <see cref="GetPriceInEffectAsync"/>.
/// </summary>
/// <param name="db">Database context of the request.</param>
public class PriceService(MoissonDbContext db)
{
    /// <summary>
    /// Returns the price of a catalog item in effect on a given day: the price with the latest effective date on or before that day.
    /// </summary>
    /// <param name="catalogItemId">Identifier of the catalog item.</param>
    /// <param name="date">Day to find the price for, such as the date a donation was received.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The price in effect on that day.</returns>
    /// <exception cref="CatalogItemNotFoundException">The catalog item does not exist.</exception>
    /// <exception cref="PriceNotFoundException">The day is before the item's first price, or the item has no price.</exception>
    public async Task<Price> GetPriceInEffectAsync(Guid catalogItemId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var item = await LoadWithPricesAsync(catalogItemId, cancellationToken);

        return PriceHistory.FindPriceInEffect(item.Prices, date)
            ?? throw new PriceNotFoundException(catalogItemId, date, item.Prices.FirstOrDefault()?.EffectiveDate);
    }

    /// <summary>
    /// Returns a catalog item with every price it has had, oldest effective date first.
    /// </summary>
    /// <param name="catalogItemId">Identifier of the catalog item.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The catalog item, with its prices sorted by effective date. The list is empty when the item has no price yet.</returns>
    /// <exception cref="CatalogItemNotFoundException">The catalog item does not exist.</exception>
    public Task<CatalogItem> GetPriceHistoryAsync(Guid catalogItemId, CancellationToken cancellationToken = default) =>
        LoadWithPricesAsync(catalogItemId, cancellationToken);

    private async Task<CatalogItem> LoadWithPricesAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        await db.CatalogItems
            .AsNoTracking()
            .Include(c => c.Prices.OrderBy(p => p.EffectiveDate))
            .SingleOrDefaultAsync(c => c.Id == catalogItemId, cancellationToken)
        ?? throw new CatalogItemNotFoundException(catalogItemId);
}