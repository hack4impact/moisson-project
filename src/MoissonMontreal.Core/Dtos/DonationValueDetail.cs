namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Value of one donated food item, with every number used to calculate it, so the calculation can be shown line by line
/// and the price and percentage can be copied onto a receipt.
/// </summary>
/// <param name="FoodItemId">Identifier of the donated food item.</param>
/// <param name="Quantity">Quantity donated.</param>
/// <param name="Unit">Unit of the quantity, which is also the unit of the price.</param>
/// <param name="UnitPrice">Price of one unit in effect on the donation date, in dollars.</param>
/// <param name="PriceEffectiveDate">Day the price used started to apply.</param>
/// <param name="QualityClass">Code of the quality class of the item, such as B.</param>
/// <param name="QualityPercent">Percentage of the price kept for the quality class, from 0 to 100.</param>
/// <param name="Value">Value of the item in dollars: quantity × unit price × quality percentage, rounded to the cent.</param>
public record DonationValueDetail(
    Guid FoodItemId,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    DateOnly PriceEffectiveDate,
    string QualityClass,
    decimal QualityPercent,
    decimal Value);
