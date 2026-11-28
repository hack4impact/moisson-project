using System.Globalization;

namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Thrown when a catalog item has no price in effect on the requested day, because the day is before its first price
/// or because the item has no price at all. The value of a donation must never be guessed in that case.
/// </summary>
public class PriceNotFoundException : Exception
{
    /// <summary>
    /// Creates the error for a catalog item and a day.
    /// </summary>
    /// <param name="catalogItemId">Identifier of the catalog item that has no price on that day.</param>
    /// <param name="date">Day a price was asked for.</param>
    /// <param name="firstEffectiveDate">Effective date of the item's first price, or <c>null</c> when the item has no price.</param>
    public PriceNotFoundException(Guid catalogItemId, DateOnly date, DateOnly? firstEffectiveDate)
        : base(BuildMessage(catalogItemId, date, firstEffectiveDate))
    {
        CatalogItemId = catalogItemId;
        Date = date;
        FirstEffectiveDate = firstEffectiveDate;
    }

    /// <summary>
    /// Identifier of the catalog item that has no price on that day.
    /// </summary>
    public Guid CatalogItemId { get; }

    /// <summary>
    /// Day a price was asked for.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Effective date of the item's first price, or <c>null</c> when the item has no price at all.
    /// </summary>
    public DateOnly? FirstEffectiveDate { get; }

    private static string BuildMessage(Guid catalogItemId, DateOnly date, DateOnly? firstEffectiveDate)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return firstEffectiveDate is { } first
            ? $"Catalog item {catalogItemId} has no price on {day}: its first price starts on {first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}."
            : $"Catalog item {catalogItemId} has no price on {day}: it has no price at all.";
    }
}