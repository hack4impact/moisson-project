using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class BcItemMappingEndpointTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private const string Url = "/api/bc-item-mappings";

    [Fact]
    public async Task List_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsTheMapping_WithTheItemDetailsAndTheConfirmer()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync("Poivron");

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest(" 1000112 ", item.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{Url}/1000112", response.Headers.Location?.AbsolutePath);
        var mapping = (await response.Content.ReadFromJsonAsync<BcItemMappingDto>())!;
        Assert.Equal(("1000112", item.Id, "Poivron"), (mapping.BusinessCentralCode, mapping.CatalogItemId, mapping.CatalogItemName));
        Assert.Equal((item.Unit, item.Category, item.QualityClassCode), (mapping.Unit, mapping.Category, mapping.QualityClass));
        Assert.False(mapping.NeedsReview);
        Assert.Equal(AuthenticatedApiFactory.Name, mapping.ConfirmedByName);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    public async Task Create_IsAllowed_ForTheSupplyTeam(string role)
    {
        using var client = await CreateClientAsync(role);
        var item = await AddCatalogItemAsync();

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest("1000094", item.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheCodeAlreadyHasAMapping()
    {
        using var client = await CreateClientAsync();
        var first = await AddCatalogItemAsync();
        var second = await AddCatalogItemAsync();
        await CreateAsync(client, "1000110", first.Id);

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest("1000110", second.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest("  ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("BusinessCentralCode", body);
        Assert.Contains("CatalogItemId", body);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTheItemIsInactive()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync(isActive: false);

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest("1000106", item.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest("1000071", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangingMappings_IsForbidden_ForFinance()
    {
        using var client = await CreateClientAsync(RoleCodes.Finance);
        var item = await AddCatalogItemAsync();

        var create = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest("1000663", item.Id));
        var update = await client.PutAsJsonAsync($"{Url}/1000663", new BcItemMappingUpdateRequest(item.Id));
        var delete = await client.DeleteAsync($"{Url}/1000663");

        Assert.Equal(
            [HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden],
            [create.StatusCode, update.StatusCode, delete.StatusCode]);
    }

    [Fact]
    public async Task ReadingMappings_IsAllowed_ForFinance()
    {
        using var client = await CreateClientAsync(RoleCodes.Finance);

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_PointsTheCodeToAnotherItem()
    {
        using var client = await CreateClientAsync();
        var before = await AddCatalogItemAsync("Tomate");
        var after = await AddCatalogItemAsync("Tomate cerise");
        await CreateAsync(client, "1000110", before.Id);

        var response = await client.PutAsJsonAsync($"{Url}/1000110", new BcItemMappingUpdateRequest(after.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mapping = (await response.Content.ReadFromJsonAsync<BcItemMappingDto>())!;
        Assert.Equal((after.Id, "Tomate cerise"), (mapping.CatalogItemId, mapping.CatalogItemName));
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheCodeHasNoMapping()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync();

        var response = await client.PutAsJsonAsync($"{Url}/UNKNOWN", new BcItemMappingUpdateRequest(item.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_FiltersTheMappingsToReview()
    {
        using var client = await CreateClientAsync();
        var active = await AddCatalogItemAsync();
        var removed = await AddCatalogItemAsync();
        await CreateAsync(client, "1000001", active.Id);
        await CreateAsync(client, "1000002", removed.Id);
        await DeactivateAsync(removed.Id);

        var all = await client.GetFromJsonAsync<List<BcItemMappingDto>>(Url);
        var toReview = await client.GetFromJsonAsync<List<BcItemMappingDto>>($"{Url}?needsReview=true");

        Assert.Equal(["1000001", "1000002"], all!.Select(m => m.BusinessCentralCode));
        Assert.Equal([false, true], all!.Select(m => m.NeedsReview));
        Assert.Equal("1000002", Assert.Single(toReview!).BusinessCentralCode);
    }

    [Fact]
    public async Task Delete_RemovesTheMapping_AndTheHistoryKeepsEveryChange()
    {
        using var client = await CreateClientAsync();
        var first = await AddCatalogItemAsync();
        var second = await AddCatalogItemAsync();
        await CreateAsync(client, "1000005", first.Id);
        await client.PutAsJsonAsync($"{Url}/1000005", new BcItemMappingUpdateRequest(second.Id));

        var delete = await client.DeleteAsync($"{Url}/1000005");
        var get = await client.GetAsync($"{Url}/1000005");
        var history = await client.GetFromJsonAsync<List<BcItemMappingChangeDto>>($"{Url}/1000005/history");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(["Deleted", "Updated", "Created"], history!.Select(c => c.Action));
        Assert.All(history!, c => Assert.Equal(AuthenticatedApiFactory.Name, c.ChangedByName));
        Assert.Equal((first.Id, second.Id), (history![1].OldCatalogItemId, history[1].NewCatalogItemId));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTheCodeHasNoMapping()
    {
        using var client = await CreateClientAsync();

        var response = await client.DeleteAsync($"{Url}/UNKNOWN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTheCodeIsTooLong()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync();
        var code = new string('1', CatalogItem.BusinessCentralCodeMaxLength + 1);

        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest(code, item.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("BusinessCentralCode", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_ReturnsTheMapping()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync("Brocoli");
        await CreateAsync(client, "1000106", item.Id);

        var response = await client.GetAsync($"{Url}/1000106");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mapping = (await response.Content.ReadFromJsonAsync<BcItemMappingDto>())!;
        Assert.Equal(("1000106", item.Id, "Brocoli"), (mapping.BusinessCentralCode, mapping.CatalogItemId, mapping.CatalogItemName));
        Assert.Equal(AuthenticatedApiFactory.Name, mapping.ConfirmedByName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTheItemIsMissing()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync();
        await CreateAsync(client, "1000112", item.Id);

        var response = await client.PutAsJsonAsync($"{Url}/1000112", new BcItemMappingUpdateRequest(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CatalogItemId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync();
        await CreateAsync(client, "1000112", item.Id);

        var response = await client.PutAsJsonAsync($"{Url}/1000112", new BcItemMappingUpdateRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTheItemIsInactive()
    {
        using var client = await CreateClientAsync();
        var item = await AddCatalogItemAsync();
        var inactive = await AddCatalogItemAsync(isActive: false);
        await CreateAsync(client, "1000112", item.Id);

        var response = await client.PutAsJsonAsync($"{Url}/1000112", new BcItemMappingUpdateRequest(inactive.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var mapping = await client.GetFromJsonAsync<BcItemMappingDto>($"{Url}/1000112");
        Assert.Equal(item.Id, mapping!.CatalogItemId);
    }

    [Fact]
    public async Task History_IsEmpty_WhenTheCodeNeverHadAMapping()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync($"{Url}/NEVER-MAPPED/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<BcItemMappingChangeDto>>())!);
    }

    // SignInAsAsync deletes every user, and the mapping tables point to users, so they are emptied first.
    private async Task<HttpClient> CreateClientAsync(string role = RoleCodes.Admin)
    {
        await using (var context = CreateContext())
        {
            await context.BcItemMappingChanges.ExecuteDeleteAsync();
            await context.BcItemMappings.ExecuteDeleteAsync();
        }

        await factory.SignInAsAsync(role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static async Task CreateAsync(HttpClient client, string code, Guid catalogItemId)
    {
        var response = await client.PostAsJsonAsync(Url, new BcItemMappingCreateRequest(code, catalogItemId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<CatalogItem> AddCatalogItemAsync(string name = "Poivron", bool isActive = true)
    {
        await using var context = CreateContext();
        var item = TestCatalogItems.Create(name);
        item.IsActive = isActive;
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    private async Task DeactivateAsync(Guid catalogItemId)
    {
        await using var context = CreateContext();
        await context.CatalogItems
            .Where(c => c.Id == catalogItemId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.IsActive, false));
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(factory.ConnectionString).Options);
}
