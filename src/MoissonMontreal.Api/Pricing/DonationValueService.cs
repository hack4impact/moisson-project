using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Api.Pricing;

/// <summary>
/// Calculates the value of a donated food item from the database: its quantity, the price of its catalog item
/// in effect on the donation date, and the percentage of its quality class. The calculation itself is <see cref="DonationValue"/>.
/// </summary>
/// <param name="db">Database context of the request.</param>
/// <param name="prices">Finds the price in effect on the donation date.</param>
public class DonationValueService(MoissonDbContext db, PriceService prices)
{
    /// <summary>
    /// Calculates the value of a donated food item and returns every number used, so the calculation can be shown and frozen on a receipt.
    /// </summary>
    /// <param name="foodItemId">Identifier of the donated food item.</param>
    /// <param name="qualityPercent">
    /// Percentage to apply, from 0 to 100, when someone chose one for this donation. When it is null, the default percentage of the
    /// item's quality class is used. How the class B percentage is chosen is still to be confirmed with Moisson Montréal.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The value of the item and the quantity, unit, price, price date, quality class and percentage used.</returns>
    /// <exception cref="FoodItemNotFoundException">The food item does not exist.</exception>
    /// <exception cref="FoodItemNotInCatalogException">The food item is not matched to a catalog item yet.</exception>
    /// <exception cref="UnitMismatchException">The food item is counted in another unit than the price of its catalog item.</exception>
    /// <exception cref="QualityPercentNotAllowedException">The percentage is outside the range of the item's quality class.</exception>
    /// <exception cref="PriceNotFoundException">The catalog item has no price in effect on the donation date.</exception>
    public async Task<DonationValueDetail> CalculateAsync(
        Guid foodItemId,
        decimal? qualityPercent = null,
        CancellationToken cancellationToken = default)
    {
        var foodItem = await db.FoodItems
            .AsNoTracking()
            .Include(item => item.QualityClass)
            .Include(item => item.CatalogItem)
            .SingleOrDefaultAsync(item => item.Id == foodItemId, cancellationToken)
            ?? throw new FoodItemNotFoundException(foodItemId);

        var catalogItem = foodItem.CatalogItem ?? throw new FoodItemNotInCatalogException(foodItemId);

        if (!IsSameUnit(foodItem.Unit, catalogItem.Unit))
        {
            throw new UnitMismatchException(foodItemId, foodItem.Unit, catalogItem.Unit);
        }

        var qualityClass = foodItem.QualityClass;
        var percent = qualityPercent ?? qualityClass.Percentage;
        if (!DonationValue.IsPercentageAllowed(qualityClass, percent))
        {
            throw new QualityPercentNotAllowedException(qualityClass.Code, percent, qualityClass.MinPercentage, qualityClass.MaxPercentage);
        }

        var price = await prices.GetPriceInEffectAsync(catalogItem.Id, foodItem.DonationDate, cancellationToken);
        var value = DonationValue.CalculateLine(foodItem.Quantity, price.UnitPrice, percent);

        return new DonationValueDetail(
            foodItem.Id,
            foodItem.Quantity,
            foodItem.Unit,
            price.UnitPrice,
            price.EffectiveDate,
            qualityClass.Code,
            percent,
            value);
    }

    /// <summary>
    /// Tells whether two units are the same, ignoring spaces around them and upper or lower case, so kg and KG match.
    /// </summary>
    /// <param name="donationUnit">Unit of the donated quantity.</param>
    /// <param name="catalogUnit">Unit of the catalog item.</param>
    /// <returns>True if both are the same unit.</returns>
    private static bool IsSameUnit(string donationUnit, string catalogUnit) =>
        string.Equals(donationUnit.Trim(), catalogUnit.Trim(), StringComparison.OrdinalIgnoreCase);
}
