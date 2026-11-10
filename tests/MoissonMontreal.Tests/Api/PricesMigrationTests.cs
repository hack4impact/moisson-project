using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class PricesMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly DateOnly December = new(2026, 12, 1);

    [Fact]
    public async Task Migrate_CreatesCatalogItemsAndPrices_OnEmptyDatabase()
    {
        await using var context = CreateContext("prices_empty");

        await context.Database.MigrateAsync();

        Assert.Equal(0, await context.CatalogItems.CountAsync());
        Assert.Equal(0, await context.Prices.CountAsync());
    }

    [Fact]
    public async Task SeveralPrices_CanExistForTheSameCatalogItem()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        await AddPriceAsync(breadId, 4.00m, November);
        await AddPriceAsync(breadId, 4.25m, December);
        await AddPriceAsync(breadId, 3.90m, new DateOnly(2027, 1, 1));

        await using var context = CreateContext();
        var prices = await context.Prices.Where(p => p.CatalogItemId == breadId).OrderBy(p => p.EffectiveDate).Select(p => p.UnitPrice).ToListAsync();

        Assert.Equal([4.00m, 4.25m, 3.90m], prices);
    }

    [Fact]
    public async Task NewPrice_IsAddedAsANewRow_AndTheEarlierPriceIsKept()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        var first = await AddPriceAsync(breadId, 4.00m, November);

        var second = await AddPriceAsync(breadId, 4.25m, December);

        await using var context = CreateContext();
        var prices = await context.Prices.Where(p => p.CatalogItemId == breadId).OrderBy(p => p.EffectiveDate).ToListAsync();
        Assert.Equal(2, prices.Count);
        Assert.NotEqual(first, second);
        Assert.Equal((first, 4.00m, November), (prices[0].Id, prices[0].UnitPrice, prices[0].EffectiveDate));
        Assert.Equal((second, 4.25m, December), (prices[1].Id, prices[1].UnitPrice, prices[1].EffectiveDate));
    }

    [Fact]
    public async Task Price_IsLinkedToItsCatalogItem()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        var priceId = await AddPriceAsync(breadId, 4.00m, November);

        await using var context = CreateContext();
        var saved = await context.Prices.Include(p => p.CatalogItem).SingleAsync(p => p.Id == priceId);

        Assert.Equal(breadId, saved.CatalogItem.Id);
        Assert.Equal("Pain", saved.CatalogItem.Name);
    }

    [Theory]
    [InlineData(2026, 11, 1)]
    [InlineData(2026, 12, 31)]
    [InlineData(2027, 1, 1)]
    [InlineData(2028, 2, 29)]
    public async Task Price_StoresItsEffectiveDateExactly(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var priceId = await AddPriceAsync(await AddCatalogItemAsync("Pain"), 4.00m, date);

        await using var context = CreateContext();
        var saved = await context.Prices.SingleAsync(p => p.Id == priceId);

        Assert.Equal(date, saved.EffectiveDate);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenTwoPricesHaveTheSameEffectiveDate()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        await AddPriceAsync(breadId, 4.00m, November);

        await using var context = CreateContext();
        context.Prices.Add(new Price { CatalogItemId = breadId, UnitPrice = 4.25m, EffectiveDate = November });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ix_prices_catalog_item_id_effective_date");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenTheSamePriceIsEnteredTwiceForTheSameDate()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        await AddPriceAsync(breadId, 4.00m, November);

        await using var context = CreateContext();
        context.Prices.Add(new Price { CatalogItemId = breadId, UnitPrice = 4.00m, EffectiveDate = November });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ix_prices_catalog_item_id_effective_date");
    }

    [Fact]
    public async Task SameEffectiveDate_IsAllowed_ForDifferentCatalogItems()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        var applesId = await AddCatalogItemAsync("Pommes");

        await AddPriceAsync(breadId, 4.00m, November);
        await AddPriceAsync(applesId, 2.49m, November);

        await using var context = CreateContext();
        Assert.Equal(2, await context.Prices.CountAsync(p => (p.CatalogItemId == breadId || p.CatalogItemId == applesId) && p.EffectiveDate == November));
    }

    [Theory]
    [InlineData("0.125", "0.125")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("0.00005", "0.0001")]
    [InlineData("0.12345", "0.1235")]
    [InlineData("99999999.9999", "99999999.9999")]
    public async Task Price_StoresUpToFourDecimals_RoundingHalfAwayFromZero(string entered, string stored)
    {
        var priceId = await AddPriceAsync(await AddCatalogItemAsync("Pain"), decimal.Parse(entered, CultureInfo.InvariantCulture), November);

        await using var context = CreateContext();
        var saved = await context.Prices.SingleAsync(p => p.Id == priceId);

        Assert.Equal(decimal.Parse(stored, CultureInfo.InvariantCulture), saved.UnitPrice);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.0001")]
    [InlineData("0.00004")]
    public async Task SaveChanges_Throws_WhenUnitPriceIsNotPositive(string unitPrice)
    {
        var breadId = await AddCatalogItemAsync("Pain");

        await using var context = CreateContext();
        context.Prices.Add(new Price { CatalogItemId = breadId, UnitPrice = decimal.Parse(unitPrice, CultureInfo.InvariantCulture), EffectiveDate = November });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_prices_unit_price_positive");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenUnitPriceIsTooLarge()
    {
        var breadId = await AddCatalogItemAsync("Pain");

        await using var context = CreateContext();
        context.Prices.Add(new Price { CatalogItemId = breadId, UnitPrice = 100_000_000m, EffectiveDate = November });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.NumericValueOutOfRange, postgresException.SqlState);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenCatalogItemDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.Prices.Add(new Price { CatalogItemId = Guid.NewGuid(), UnitPrice = 4.00m, EffectiveDate = November });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_prices_catalog_items_catalog_item_id");
    }

    [Fact]
    public async Task DeleteCatalogItem_Throws_WhenItHasPrices()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        await AddPriceAsync(breadId, 4.00m, November);

        await using var context = CreateContext();
        context.CatalogItems.Remove(await context.CatalogItems.SingleAsync(c => c.Id == breadId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_prices_catalog_items_catalog_item_id");
    }

    [Fact]
    public async Task DeleteCatalogItem_Succeeds_WhenItHasNoPrice()
    {
        var breadId = await AddCatalogItemAsync("Pain");

        await using (var deleteContext = CreateContext())
        {
            deleteContext.CatalogItems.Remove(await deleteContext.CatalogItems.SingleAsync(c => c.Id == breadId));
            await deleteContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        Assert.False(await context.CatalogItems.AnyAsync(c => c.Id == breadId));
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenCatalogItemNameIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.CatalogItems.Add(new CatalogItem { Name = "" });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_catalog_items_name_not_empty");
    }

    [Fact]
    public async Task CatalogItemAndPrice_SetCreatedAt()
    {
        var breadId = await AddCatalogItemAsync("Pain");
        var priceId = await AddPriceAsync(breadId, 4.00m, November);

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(c => c.Id == breadId);
        var price = await context.Prices.SingleAsync(p => p.Id == priceId);

        Assert.NotEqual(default, item.CreatedAt);
        Assert.NotEqual(default, price.CreatedAt);
        Assert.Equal(TimeSpan.Zero, price.CreatedAt.Offset);
    }

    private async Task<Guid> AddCatalogItemAsync(string name)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var item = new CatalogItem { Name = name };
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();

        return item.Id;
    }

    private async Task<Guid> AddPriceAsync(Guid catalogItemId, decimal unitPrice, DateOnly effectiveDate)
    {
        await using var context = CreateContext();
        var price = new Price { CatalogItemId = catalogItemId, UnitPrice = unitPrice, EffectiveDate = effectiveDate };
        context.Prices.Add(price);
        await context.SaveChangesAsync();

        return price.Id;
    }

    private MoissonDbContext CreateContext(string? database = null)
    {
        var connectionString = postgres.ConnectionString;
        if (database is not null)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            builder["Database"] = database;
            connectionString = builder.ConnectionString;
        }

        return new MoissonDbContext(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
    }
}
