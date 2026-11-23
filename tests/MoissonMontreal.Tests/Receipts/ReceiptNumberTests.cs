using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Tests.Receipts;

public class ReceiptNumberTests
{
    [Theory]
    [InlineData(1, "DEN_000001")]
    [InlineData(42, "DEN_000042")]
    [InlineData(56836, "DEN_056836")]
    [InlineData(999999, "DEN_999999")]
    public void Format_PutsTheSeriesBeforeSixDigits(int sequence, string expected)
    {
        Assert.Equal(expected, ReceiptNumber.Format(sequence));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void Format_Throws_WhenTheSequenceIsOutOfRange(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReceiptNumber.Format(sequence));
    }

    [Fact]
    public void Format_NeverUsesTheMoneyDonationSeries()
    {
        Assert.DoesNotContain("WEB", ReceiptNumber.Format(1), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_FitsTheDatabaseColumn()
    {
        Assert.Equal(ReceiptNumber.MaxLength, ReceiptNumber.Format(ReceiptNumber.LastSequence).Length);
    }
}
