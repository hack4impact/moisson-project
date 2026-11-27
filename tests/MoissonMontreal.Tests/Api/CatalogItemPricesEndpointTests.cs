using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class CatalogItemPricesEndpointTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>, IAsyncLifetime
{
    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly DateOnly December = new(2026, 12, 1);

    // Every route requires an active account, whatever its role.
    public Task InitializeAsync() => factory.ReplaceUsersWithAsync(RoleCodes.Supply);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetHistory_ReturnsEveryPrice_OldestFirst()
    {
        var breadId = await AddCatalogItemAsync("Pain", (4.25m, December), (4.00m, November));

        var response = await GetAsync($"/api/catalog-items/{breadId}/prices");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<PriceHistoryDto>();
        Assert.NotNull(history);
        Assert.Equal((breadId, "Pain"), (history.CatalogItemId, history.CatalogItemName));
        Assert.Equal([November, December], history.Prices.Select(p => p.EffectiveDate));
        Assert.Equal([4.00m, 4.25m], history.Prices.Select(p => p.UnitPrice));
        Assert.All(history.Prices, p => Assert.NotEqual(Guid.Empty, p.Id));
    }

    [Fact]
    public async Task GetHistory_ReturnsAnEmptyList_WhenTheItemHasNoPrice()
    {
        var applesId = await AddCatalogItemAsync("Pommes");

        var history = await (await GetAsync($"/api/catalog-items/{applesId}/prices")).Content.ReadFromJsonAsync<PriceHistoryDto>();

        Assert.NotNull(history);
        Assert.Empty(history.Prices);
    }

    [Fact]
    public async Task GetHistory_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        var response = await GetAsync($"/api/catalog-items/{Guid.NewGuid()}/prices");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Catalog item not found", (await ReadProblemAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetHistory_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var breadId = await AddCatalogItemAsync("Pain", (4.00m, November));

        var response = await GetAsync($"/api/catalog-items/{breadId}/prices", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("2026-11-01", 4.00)]
    [InlineData("2026-11-30", 4.00)]
    [InlineData("2026-12-01", 4.25)]
    [InlineData("2027-03-10", 4.25)]
    public async Task GetPriceInEffect_ReturnsThePriceThatAppliesOnTheDate(string date, decimal expected)
    {
        var breadId = await AddCatalogItemAsync("Pain", (4.00m, November), (4.25m, December));

        var response = await GetAsync($"/api/catalog-items/{breadId}/prices/in-effect?date={date}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var price = await response.Content.ReadFromJsonAsync<PriceDto>();
        Assert.Equal(expected, price!.UnitPrice);
    }

    [Fact]
    public async Task GetPriceInEffect_ReturnsAClearError_WhenTheDateIsBeforeTheFirstPrice()
    {
        var breadId = await AddCatalogItemAsync("Pain", (4.00m, November));

        var response = await GetAsync($"/api/catalog-items/{breadId}/prices/in-effect?date=2026-10-20");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("No price on this date", problem.GetProperty("title").GetString());
        Assert.Equal(
            $"Catalog item {breadId} has no price on 2026-10-20: its first price starts on 2026-11-01.",
            problem.GetProperty("detail").GetString());
        Assert.Equal("2026-11-01", problem.GetProperty("firstEffectiveDate").GetString());
        Assert.False(problem.TryGetProperty("unitPrice", out _));
    }

    [Fact]
    public async Task GetPriceInEffect_ReturnsAClearError_WhenTheItemHasNoPrice()
    {
        var applesId = await AddCatalogItemAsync("Pommes");

        var response = await GetAsync($"/api/catalog-items/{applesId}/prices/in-effect?date=2026-11-15");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("firstEffectiveDate").ValueKind);
    }

    [Fact]
    public async Task GetPriceInEffect_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        var response = await GetAsync($"/api/catalog-items/{Guid.NewGuid()}/prices/in-effect?date=2026-11-15");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Catalog item not found", (await ReadProblemAsync(response)).GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?date=")]
    [InlineData("?date=not-a-date")]
    [InlineData("?date=2026-13-01")]
    public async Task GetPriceInEffect_ReturnsBadRequest_WhenTheDateIsMissingOrInvalid(string query)
    {
        var breadId = await AddCatalogItemAsync("Pain", (4.00m, November));

        var response = await GetAsync($"/api/catalog-items/{breadId}/prices/in-effect{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? token = "")
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token == "" ? factory.CreateToken() : token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> AddCatalogItemAsync(string name, params (decimal UnitPrice, DateOnly EffectiveDate)[] prices)
    {
        await using var context = factory.CreateDbContext();
        var item = TestCatalogItems.Create(name);
        foreach (var (unitPrice, effectiveDate) in prices)
        {
            item.Prices.Add(new Price { UnitPrice = unitPrice, EffectiveDate = effectiveDate });
        }

        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }
}