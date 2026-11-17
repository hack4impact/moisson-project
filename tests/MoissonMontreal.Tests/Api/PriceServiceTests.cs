using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Pricing;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class PriceServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly DateOnly December = new(2026, 12, 1);
    private static readonly DateOnly January = new(2027, 1, 1);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("2026-11-01", 4.00)]
    [InlineData("2026-11-15", 4.00)]
    [InlineData("2026-11-30", 4.00)]
    [InlineData("2026-12-01", 4.25)]
    [InlineData("2027-01-01", 3.90)]
    [InlineData("2027-03-10", 3.90)]
    public async Task GetPriceInEffect_ReturnsThePriceThatAppliesOnTheDate(string date, decimal expected)
    {
        var breadId = await AddBreadWithHistoryAsync();

        await using var context = CreateContext();
        var price = await new PriceService(context).GetPriceInEffectAsync(breadId, DateOnly.Parse(date));

        Assert.Equal(expected, price.UnitPrice);
        Assert.Equal(breadId, price.CatalogItemId);
    }

    [Fact]
    public async Task GetPriceInEffect_ReturnsThePrice_OnItsExactEffectiveDate()
    {
        var breadId = await AddBreadWithHistoryAsync();

        await using var context = CreateContext();
        var price = await new PriceService(context).GetPriceInEffectAsync(breadId, December);

        Assert.Equal((4.25m, December), (price.UnitPrice, price.EffectiveDate));
    }

    [Fact]
    public async Task GetPriceInEffect_Throws_WhenTheDateIsBeforeTheFirstPrice()
    {
        var breadId = await AddBreadWithHistoryAsync();
        var dayBefore = November.AddDays(-1);

        await using var context = CreateContext();
        var e = await Assert.ThrowsAsync<PriceNotFoundException>(
            () => new PriceService(context).GetPriceInEffectAsync(breadId, dayBefore));

        Assert.Equal((breadId, dayBefore, November), (e.CatalogItemId, e.Date, e.FirstEffectiveDate));
    }

    [Fact]
    public async Task GetPriceInEffect_Throws_WhenTheItemHasNoPrice()
    {
        var applesId = await AddCatalogItemAsync("Pommes");

        await using var context = CreateContext();
        var e = await Assert.ThrowsAsync<PriceNotFoundException>(
            () => new PriceService(context).GetPriceInEffectAsync(applesId, December));

        Assert.Null(e.FirstEffectiveDate);
    }

    [Fact]
    public async Task GetPriceInEffect_Throws_WhenTheItemDoesNotExist()
    {
        var unknownId = Guid.NewGuid();

        await using var context = CreateContext();
        var e = await Assert.ThrowsAsync<CatalogItemNotFoundException>(
            () => new PriceService(context).GetPriceInEffectAsync(unknownId, December));

        Assert.Equal(unknownId, e.CatalogItemId);
    }

    [Fact]
    public async Task GetPriceInEffect_IgnoresThePricesOfOtherItems()
    {
        var breadId = await AddBreadWithHistoryAsync();
        var applesId = await AddCatalogItemAsync("Pommes");
        await AddPriceAsync(applesId, 2.49m, December);

        await using var context = CreateContext();
        var service = new PriceService(context);

        Assert.Equal(4.00m, (await service.GetPriceInEffectAsync(breadId, December.AddDays(-1))).UnitPrice);
        await Assert.ThrowsAsync<PriceNotFoundException>(() => service.GetPriceInEffectAsync(applesId, November));
    }

    [Fact]
    public async Task GetPriceHistory_ReturnsEveryPrice_OldestFirst()
    {
        var itemId = await AddCatalogItemAsync("Pain");
        await AddPriceAsync(itemId, 3.90m, January);
        await AddPriceAsync(itemId, 4.00m, November);
        await AddPriceAsync(itemId, 4.25m, December);

        await using var context = CreateContext();
        var item = await new PriceService(context).GetPriceHistoryAsync(itemId);

        Assert.Equal("Pain", item.Name);
        Assert.Equal([November, December, January], item.Prices.Select(p => p.EffectiveDate));
        Assert.Equal([4.00m, 4.25m, 3.90m], item.Prices.Select(p => p.UnitPrice));
    }

    [Fact]
    public async Task GetPriceHistory_ReturnsAnEmptyList_WhenTheItemHasNoPrice()
    {
        var applesId = await AddCatalogItemAsync("Pommes");

        await using var context = CreateContext();
        var item = await new PriceService(context).GetPriceHistoryAsync(applesId);

        Assert.Empty(item.Prices);
    }

    [Fact]
    public async Task GetPriceHistory_Throws_WhenTheItemDoesNotExist()
    {
        await using var context = CreateContext();

        await Assert.ThrowsAsync<CatalogItemNotFoundException>(
            () => new PriceService(context).GetPriceHistoryAsync(Guid.NewGuid()));
    }

    private async Task<Guid> AddBreadWithHistoryAsync()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        await AddPriceAsync(breadId, 4.00m, November);
        await AddPriceAsync(breadId, 4.25m, December);
        await AddPriceAsync(breadId, 3.90m, January);
        return breadId;
    }

    private async Task<Guid> AddCatalogItemAsync(string name)
    {
        await using var context = CreateContext();
        var item = new CatalogItem { Name = name };
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    private async Task AddPriceAsync(Guid catalogItemId, decimal unitPrice, DateOnly effectiveDate)
    {
        await using var context = CreateContext();
        context.Prices.Add(new Price { CatalogItemId = catalogItemId, UnitPrice = unitPrice, EffectiveDate = effectiveDate });
        await context.SaveChangesAsync();
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}