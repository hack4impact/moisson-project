using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Rule that picks, in the price history of one catalog item, the price in effect on a given day.
/// </summary>
public static class PriceHistory
{
    /// <summary>
    /// Returns the price with the latest effective date on or before <paramref name="date"/>.
    /// A price applies from its effective date itself until the day before the next price starts.
    /// </summary>
    /// <param name="prices">Every price of one catalog item, in any order.</param>
    /// <param name="date">Day to find the price for, such as the date a donation was received.</param>
    /// <returns>The price in effect on that day, or <c>null</c> when the day is before the first price or the item has no price.</returns>
    public static Price? FindPriceInEffect(IEnumerable<Price> prices, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(prices);

        return prices
            .Where(p => p.EffectiveDate <= date)
            .MaxBy(p => p.EffectiveDate);
    }

    /// <summary>
    /// Returns the earliest effective date in the price history of one catalog item.
    /// </summary>
    /// <param name="prices">Every price of one catalog item, in any order.</param>
    /// <returns>The first effective date, or <c>null</c> when the item has no price.</returns>
    public static DateOnly? FindFirstEffectiveDate(IEnumerable<Price> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        return prices.Min(p => (DateOnly?)p.EffectiveDate);
    }
}