using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Rules that give the value of donated food: quantity × unit price × quality percentage, rounded to the cent.
/// The rounding rule is the one described in docs/database/receipts.md: each line is rounded to the cent,
/// a value exactly halfway between two cents is rounded away from zero, and a total is the sum of the rounded lines.
/// </summary>
public static class DonationValue
{
    /// <summary>
    /// Highest quality percentage, which keeps the full price.
    /// </summary>
    private const decimal FullPercentage = 100m;

    /// <summary>
    /// Calculates the value of one donation line.
    /// </summary>
    /// <param name="quantity">Quantity donated, in the unit of the price. Must be greater than zero.</param>
    /// <param name="unitPrice">Price of one unit, in dollars. Must be greater than zero.</param>
    /// <param name="qualityPercent">Share of the price kept for the quality class, from 0 to 100, such as 75 for 75 %.</param>
    /// <returns>The value of the line in dollars, rounded to the cent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The quantity or the price is zero or negative, or the percentage is outside 0 to 100.</exception>
    public static decimal CalculateLine(decimal quantity, decimal unitPrice, decimal qualityPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(qualityPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(qualityPercent, FullPercentage);

        return RoundToCents(quantity * unitPrice * qualityPercent / FullPercentage);
    }

    /// <summary>
    /// Calculates the total value of several donation lines. Each line is rounded to the cent before the lines are added,
    /// so the total always matches the line-by-line detail shown to the user.
    /// </summary>
    /// <param name="lines">The lines to add, each with its quantity, unit price and quality percentage.</param>
    /// <returns>The sum of the rounded line values, in dollars. Zero when there is no line.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A line has a quantity or a price of zero or less, or a percentage outside 0 to 100.</exception>
    public static decimal CalculateTotal(IEnumerable<DonationLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines.Sum(line => CalculateLine(line.Quantity, line.UnitPrice, line.QualityPercent));
    }

    /// <summary>
    /// Tells whether a percentage is allowed for a quality class, between its lowest and highest percentage included.
    /// For example, class B allows 50 % to 75 % today.
    /// </summary>
    /// <param name="qualityClass">Quality class of the donated food.</param>
    /// <param name="qualityPercent">Percentage to check, from 0 to 100.</param>
    /// <returns>True if the percentage is within the range of the class.</returns>
    public static bool IsPercentageAllowed(QualityClass qualityClass, decimal qualityPercent)
    {
        ArgumentNullException.ThrowIfNull(qualityClass);

        return qualityPercent >= qualityClass.MinPercentage && qualityPercent <= qualityClass.MaxPercentage;
    }

    /// <summary>
    /// Rounds an amount to the nearest cent. An amount exactly halfway, such as $0.225, is rounded away from zero, to $0.23.
    /// The default Math.Round would round it to the even cent, $0.22, which is not the rule of the project.
    /// </summary>
    /// <param name="amount">The amount to round, in dollars.</param>
    /// <returns>The amount rounded to 2 decimals.</returns>
    private static decimal RoundToCents(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// One donation line to value: the quantity, the price of one unit and the quality percentage.
/// </summary>
/// <param name="Quantity">Quantity donated, in the unit of the price.</param>
/// <param name="UnitPrice">Price of one unit, in dollars.</param>
/// <param name="QualityPercent">Share of the price kept for the quality class, from 0 to 100.</param>
public readonly record struct DonationLine(decimal Quantity, decimal UnitPrice, decimal QualityPercent);
