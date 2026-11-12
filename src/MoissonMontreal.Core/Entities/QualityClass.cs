namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Quality class of donated food, with the share of the price kept when a donation is valued.
/// The percentage applied must stay between <see cref="MinPercentage"/> and <see cref="MaxPercentage"/>.
/// </summary>
public class QualityClass
{
    /// <summary>
    /// One-letter code of the class, such as A, B or C. It identifies the class and never changes.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Name of the class shown in the app, such as Imperfect. Two classes cannot share a name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of the price applied by default, from 0 to 100, with up to two decimal places.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Lowest percentage allowed for this class, from 0 to 100.
    /// </summary>
    public decimal MinPercentage { get; set; }

    /// <summary>
    /// Highest percentage allowed for this class, from 0 to 100.
    /// </summary>
    public decimal MaxPercentage { get; set; }
}
