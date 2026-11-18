using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Tests.Pricing;

public class PriceHistoryTests
{
    // Bread history from docs/database/prices.md.
    private static readonly Price November = NewPrice(4.00m, new DateOnly(2026, 11, 1));
    private static readonly Price December = NewPrice(4.25m, new DateOnly(2026, 12, 1));
    private static readonly Price January = NewPrice(3.90m, new DateOnly(2027, 1, 1));
    private static readonly Price[] Bread = [November, December, January];

    public static TheoryData<DateOnly, decimal> DatesAndExpectedPrices() => new()
    {
        { new DateOnly(2026, 11, 1), 4.00m },   // exact effective date of the first price
        { new DateOnly(2026, 11, 15), 4.00m },  // between two prices
        { new DateOnly(2026, 11, 30), 4.00m },  // last day before the next price
        { new DateOnly(2026, 12, 1), 4.25m },   // exact effective date of a later price
        { new DateOnly(2026, 12, 31), 4.25m },
        { new DateOnly(2027, 1, 1), 3.90m },    // exact effective date of the latest price
        { new DateOnly(2027, 3, 10), 3.90m },   // after the latest price
        { new DateOnly(2099, 12, 31), 3.90m },  // far in the future
    };

    [Theory]
    [MemberData(nameof(DatesAndExpectedPrices))]
    public void FindPriceInEffect_ReturnsTheLatestPriceOnOrBeforeTheDate(DateOnly date, decimal expected)
    {
        var price = PriceHistory.FindPriceInEffect(Bread, date);

        Assert.NotNull(price);
        Assert.Equal(expected, price.UnitPrice);
    }

    [Theory]
    [InlineData(2026, 10, 31)] // the day before the first price
    [InlineData(2026, 10, 20)]
    [InlineData(1, 1, 1)]
    public void FindPriceInEffect_ReturnsNull_WhenTheDateIsBeforeTheFirstPrice(int year, int month, int day)
    {
        Assert.Null(PriceHistory.FindPriceInEffect(Bread, new DateOnly(year, month, day)));
    }

    [Fact]
    public void FindPriceInEffect_ReturnsNull_WhenThereIsNoPrice()
    {
        Assert.Null(PriceHistory.FindPriceInEffect([], new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void FindPriceInEffect_DoesNotDependOnTheOrderOfThePrices()
    {
        Price[] shuffled = [January, November, December];

        Assert.Same(December, PriceHistory.FindPriceInEffect(shuffled, new DateOnly(2026, 12, 15)));
        Assert.Same(November, PriceHistory.FindPriceInEffect(shuffled, new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void FindPriceInEffect_ReturnsTheOnlyPrice_FromItsEffectiveDate()
    {
        Price[] single = [November];

        Assert.Same(November, PriceHistory.FindPriceInEffect(single, new DateOnly(2026, 11, 1)));
        Assert.Null(PriceHistory.FindPriceInEffect(single, new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void FindFirstEffectiveDate_DoesNotDependOnTheOrderOfThePrices()
    {
        Price[] shuffled = [January, November, December];

        Assert.Equal(new DateOnly(2026, 11, 1), PriceHistory.FindFirstEffectiveDate(shuffled));
    }

    [Fact]
    public void FindFirstEffectiveDate_ReturnsNull_WhenThereIsNoPrice()
    {
        Assert.Null(PriceHistory.FindFirstEffectiveDate([]));
    }

    [Fact]
    public void FindFirstEffectiveDate_Throws_WhenPricesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PriceHistory.FindFirstEffectiveDate(null!));
    }

    [Fact]
    public void FindPriceInEffect_Throws_WhenPricesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PriceHistory.FindPriceInEffect(null!, new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void PriceNotFoundException_NamesTheFirstEffectiveDate()
    {
        var id = Guid.NewGuid();

        var e = new PriceNotFoundException(id, new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 1));

        Assert.Equal($"Catalog item {id} has no price on 2026-10-20: its first price starts on 2026-11-01.", e.Message);
        Assert.Equal((id, new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 1)), (e.CatalogItemId, e.Date, e.FirstEffectiveDate));
    }

    [Fact]
    public void PriceNotFoundException_SaysWhenTheItemHasNoPrice()
    {
        var id = Guid.NewGuid();

        var e = new PriceNotFoundException(id, new DateOnly(2026, 11, 15), null);

        Assert.Equal($"Catalog item {id} has no price on 2026-11-15: it has no price at all.", e.Message);
        Assert.Null(e.FirstEffectiveDate);
    }

    private static Price NewPrice(decimal unitPrice, DateOnly effectiveDate) => new()
    {
        Id = Guid.NewGuid(),
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
    };
}