using System.Globalization;

namespace MoissonMontreal.Core.Receipts;

/// <summary>
/// Format of the number given to a receipt when it is issued: the series, an underscore and six digits, such as DEN_000001.
/// The series must differ from every other receipt series of Moisson Montréal (for example WEB_ for money donations),
/// so that two receipts of the organization never share a number.
/// </summary>
public static class ReceiptNumber
{
    public const string Series = "DEN";

    public const int Digits = 6;

    public const int MaxLength = 10;

    public const int LastSequence = 999_999;

    public static string Format(int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, LastSequence);

        return $"{Series}_{sequence.ToString($"D{Digits}", CultureInfo.InvariantCulture)}";
    }
}
