using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Pricing;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

// Bread costs $4.00 per kg from November 1 and $4.25 from December 1. Quality classes come from the migration:
// A = 100 %, B = 50 % by default (50 % to 75 % allowed), C = 0 %.
// Every expected value is computed by hand in the comment above it, so it can be checked again for issue #33.
[Trait("Category", "Integration")]
public class DonationValueServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateOnly November1 = new(2026, 11, 1);
    private static readonly DateOnly November15 = new(2026, 11, 15);
    private static readonly DateOnly December1 = new(2026, 12, 1);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Calculate_UsesThePriceInEffectOnTheDonationDate_AndTheDefaultPercentageOfTheClass()
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 12.50m, "kg", November15, "B");

        var detail = await CalculateAsync(donation);

        // November 15 is before the December price, so $4.00 applies. Class B default: 50 %.
        // 12.50 kg × $4.00 × 50 % = $25.00
        Assert.Equal(
            new DonationValueDetail(donation, 12.50m, "kg", 4.00m, November1, "B", 50m, 25.00m),
            detail);
    }

    [Fact]
    public async Task Calculate_UsesTheNewPrice_FromItsEffectiveDate()
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 12.50m, "kg", December1, "A");

        var detail = await CalculateAsync(donation);

        // December 1 is the effective date of the new price, so $4.25 applies. Class A: 100 %.
        // 12.50 kg × $4.25 × 100 % = $53.125, exactly halfway, so $53.13
        Assert.Equal((4.25m, December1, 100m, 53.13m), (detail.UnitPrice, detail.PriceEffectiveDate, detail.QualityPercent, detail.Value));
    }

    [Fact]
    public async Task Calculate_UsesTheChosenPercentage_WhenItIsWithinTheRangeOfTheClass()
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 12.50m, "kg", November15, "B");

        var detail = await CalculateAsync(donation, qualityPercent: 75m);

        // 12.50 kg × $4.00 × 75 % = $37.50
        Assert.Equal((75m, 37.50m), (detail.QualityPercent, detail.Value));
    }

    [Fact]
    public async Task Calculate_GivesZero_ForClassC()
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 12.50m, "kg", November15, "C");

        var detail = await CalculateAsync(donation);

        // 12.50 kg × $4.00 × 0 % = $0.00
        Assert.Equal((0m, 0.00m), (detail.QualityPercent, detail.Value));
    }

    [Fact]
    public async Task Calculate_AcceptsTheSameUnit_WithAnotherCase()
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 12.50m, "KG", November15, "A");

        var detail = await CalculateAsync(donation);

        // 12.50 kg × $4.00 × 100 % = $50.00
        Assert.Equal(50.00m, detail.Value);
    }

    [Theory]
    [InlineData("B", 49.99)]
    [InlineData("B", 75.01)]
    [InlineData("B", 80)]
    [InlineData("A", 75)]
    public async Task Calculate_Throws_WhenTheChosenPercentageIsOutsideTheRangeOfTheClass(string quality, decimal qualityPercent)
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 12.50m, "kg", November15, quality);

        var e = await Assert.ThrowsAsync<QualityPercentNotAllowedException>(() => CalculateAsync(donation, qualityPercent));

        Assert.Equal((quality, qualityPercent), (e.QualityClass, e.QualityPercent));
    }

    [Fact]
    public async Task Calculate_Throws_WhenTheUnitDiffersFromTheCatalogItem()
    {
        var bread = await AddBreadAsync();
        var donation = await AddDonationAsync(bread, 3m, "caisse", November15, "A");

        var e = await Assert.ThrowsAsync<UnitMismatchException>(() => CalculateAsync(donation));

        Assert.Equal((donation, "caisse", "kg"), (e.FoodItemId, e.DonationUnit, e.CatalogUnit));
    }

    [Fact]
    public async Task Calculate_Throws_WhenTheFoodItemIsNotMatchedToTheCatalog()
    {
        var donation = await AddDonationAsync(catalogItemId: null, 12.50m, "kg", November15, "A");

        var e = await Assert.ThrowsAsync<FoodItemNotInCatalogException>(() => CalculateAsync(donation));

        Assert.Equal(donation, e.FoodItemId);
    }

    [Fact]
    public async Task Calculate_Throws_WhenThereIsNoPriceOnTheDonationDate()
    {
        var bread = await AddBreadAsync();
        var dayBefore = November1.AddDays(-1);
        var donation = await AddDonationAsync(bread, 12.50m, "kg", dayBefore, "A");

        var e = await Assert.ThrowsAsync<PriceNotFoundException>(() => CalculateAsync(donation));

        Assert.Equal((bread, dayBefore, November1), (e.CatalogItemId, e.Date, e.FirstEffectiveDate));
    }

    [Fact]
    public async Task Calculate_Throws_WhenTheFoodItemDoesNotExist()
    {
        var unknownId = Guid.NewGuid();

        var e = await Assert.ThrowsAsync<FoodItemNotFoundException>(() => CalculateAsync(unknownId));

        Assert.Equal(unknownId, e.FoodItemId);
    }

    private async Task<DonationValueDetail> CalculateAsync(Guid foodItemId, decimal? qualityPercent = null)
    {
        await using var context = CreateContext();
        return await new DonationValueService(context, new PriceService(context)).CalculateAsync(foodItemId, qualityPercent);
    }

    private async Task<Guid> AddBreadAsync()
    {
        await using var context = CreateContext();
        var bread = TestCatalogItems.Create("Pain");
        context.CatalogItems.Add(bread);
        context.Prices.Add(new Price { CatalogItem = bread, UnitPrice = 4.00m, EffectiveDate = November1 });
        context.Prices.Add(new Price { CatalogItem = bread, UnitPrice = 4.25m, EffectiveDate = December1 });
        await context.SaveChangesAsync();
        return bread.Id;
    }

    private async Task<Guid> AddDonationAsync(Guid? catalogItemId, decimal quantity, string unit, DateOnly donationDate, string quality)
    {
        await using var context = CreateContext();
        var donor = new Donor { Name = "Boulangerie du coin" };
        var donation = new FoodItem
        {
            Donor = donor,
            Name = "Pain",
            Quantity = quantity,
            Unit = unit,
            DonationDate = donationDate,
            Quality = quality,
            CatalogItemId = catalogItemId,
        };
        context.FoodItems.Add(donation);
        await context.SaveChangesAsync();
        return donation.Id;
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}
