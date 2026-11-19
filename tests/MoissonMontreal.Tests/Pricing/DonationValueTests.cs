using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Tests.Pricing;

// Every expected value is computed by hand in the comment above it, so it can be checked again for issue #33.
public class DonationValueTests
{
    [Fact]
    public void CalculateLine_BreadOfClassB()
    {
        // 12.50 kg × $4.00 × 75 % = 50.00 × 0.75 = $37.50
        Assert.Equal(37.50m, DonationValue.CalculateLine(12.50m, 4.00m, 75m));
    }

    [Fact]
    public void CalculateLine_ApplesOfClassA()
    {
        // 3 cases × $2.49 × 100 % = $7.47
        Assert.Equal(7.47m, DonationValue.CalculateLine(3m, 2.49m, 100m));
    }

    [Fact]
    public void CalculateLine_RoundsHalfACentAwayFromZero()
    {
        // 1 kg × $0.45 × 50 % = $0.225, exactly halfway, so $0.23 and not $0.22
        Assert.Equal(0.23m, DonationValue.CalculateLine(1m, 0.45m, 50m));
    }

    [Fact]
    public void CalculateLine_KeepsTheFourDecimalsOfThePrice()
    {
        // 7 kg × $0.125 × 100 % = $0.875, exactly halfway, so $0.88
        Assert.Equal(0.88m, DonationValue.CalculateLine(7m, 0.125m, 100m));
    }

    [Fact]
    public void CalculateLine_RoundsDownBelowHalfACent()
    {
        // 3 × $1.00 × 66.67 % = $2.0001, so $2.00
        Assert.Equal(2.00m, DonationValue.CalculateLine(3m, 1.00m, 66.67m));
    }

    [Fact]
    public void CalculateLine_GivesZeroForClassC()
    {
        // 10 × $3.00 × 0 % = $0.00
        Assert.Equal(0.00m, DonationValue.CalculateLine(10m, 3.00m, 0m));
    }

    [Fact]
    public void CalculateLine_HasNoBinaryRoundingError()
    {
        // 3 × $0.10 × 100 % = $0.30 exactly.
        Assert.Equal(0.30m, DonationValue.CalculateLine(3m, 0.10m, 100m));
    }

    [Fact]
    public void CalculateTotal_AddsTheLines()
    {
        // $37.50 (bread) + $7.47 (apples) = $44.97
        DonationLine[] lines = [new(12.50m, 4.00m, 75m), new(3m, 2.49m, 100m)];

        Assert.Equal(44.97m, DonationValue.CalculateTotal(lines));
    }

    [Fact]
    public void CalculateTotal_RoundsEachLineBeforeAddingThem()
    {
        // Each line: 1 × $0.45 × 50 % = $0.225, rounded to $0.23. Total: $0.23 + $0.23 = $0.46.
        // Adding before rounding would give $0.45, which would not match the detail of the lines.
        DonationLine[] lines = [new(1m, 0.45m, 50m), new(1m, 0.45m, 50m)];

        Assert.Equal(0.46m, DonationValue.CalculateTotal(lines));
    }

    [Fact]
    public void CalculateTotal_IsZero_WhenThereIsNoLine()
    {
        Assert.Equal(0m, DonationValue.CalculateTotal([]));
    }

    [Theory]
    [InlineData(0, 4.00, 75)]
    [InlineData(-1, 4.00, 75)]
    [InlineData(12.50, 0, 75)]
    [InlineData(12.50, -4.00, 75)]
    [InlineData(12.50, 4.00, -1)]
    [InlineData(12.50, 4.00, 100.01)]
    public void CalculateLine_RefusesImpossibleValues(decimal quantity, decimal unitPrice, decimal qualityPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DonationValue.CalculateLine(quantity, unitPrice, qualityPercent));
    }

    [Fact]
    public void CalculateTotal_RefusesALineWithImpossibleValues()
    {
        DonationLine[] lines = [new(12.50m, 4.00m, 75m), new(0m, 2.49m, 100m)];

        Assert.Throws<ArgumentOutOfRangeException>(() => DonationValue.CalculateTotal(lines));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(62.50, true)]
    [InlineData(75, true)]
    [InlineData(49.99, false)]
    [InlineData(75.01, false)]
    [InlineData(100, false)]
    public void IsPercentageAllowed_ChecksTheRangeOfClassB(decimal qualityPercent, bool expected)
    {
        var classB = new QualityClass { Code = "B", Percentage = 50m, MinPercentage = 50m, MaxPercentage = 75m };

        Assert.Equal(expected, DonationValue.IsPercentageAllowed(classB, qualityPercent));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(99.99, false)]
    public void IsPercentageAllowed_OnlyAllowsOneHundredForClassA(decimal qualityPercent, bool expected)
    {
        var classA = new QualityClass { Code = "A", Percentage = 100m, MinPercentage = 100m, MaxPercentage = 100m };

        Assert.Equal(expected, DonationValue.IsPercentageAllowed(classA, qualityPercent));
    }
}
