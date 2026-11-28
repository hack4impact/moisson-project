using System.Globalization;

namespace MoissonMontreal.Core.Pricing;

/// <summary>
/// Thrown when the quality percentage asked for a donated food item is outside the range of its quality class,
/// such as 80 % for class B, which allows 50 % to 75 % today.
/// </summary>
public class QualityPercentNotAllowedException : Exception
{
    /// <summary>
    /// Creates the error for a quality class and the percentage that was refused.
    /// </summary>
    /// <param name="qualityClass">Code of the quality class, such as B.</param>
    /// <param name="qualityPercent">Percentage that was refused.</param>
    /// <param name="minPercentage">Lowest percentage allowed for the class.</param>
    /// <param name="maxPercentage">Highest percentage allowed for the class.</param>
    public QualityPercentNotAllowedException(string qualityClass, decimal qualityPercent, decimal minPercentage, decimal maxPercentage)
        : base(string.Create(
            CultureInfo.InvariantCulture,
            $"A quality percentage of {qualityPercent} % is not allowed for class {qualityClass}, which allows {minPercentage} % to {maxPercentage} %."))
    {
        QualityClass = qualityClass;
        QualityPercent = qualityPercent;
        MinPercentage = minPercentage;
        MaxPercentage = maxPercentage;
    }

    /// <summary>
    /// Code of the quality class, such as B.
    /// </summary>
    public string QualityClass { get; }

    /// <summary>
    /// Percentage that was refused.
    /// </summary>
    public decimal QualityPercent { get; }

    /// <summary>
    /// Lowest percentage allowed for the class.
    /// </summary>
    public decimal MinPercentage { get; }

    /// <summary>
    /// Highest percentage allowed for the class.
    /// </summary>
    public decimal MaxPercentage { get; }
}
