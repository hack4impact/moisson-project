using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class CatalogEndpointTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task List_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/catalog-items");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsTheCatalogOrderedByName()
    {
        await ClearAsync();
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(name: "Pommes", code: "BC-APPLES"));
        await CreateAsync(client, Request(name: "Pain", code: "BC-BREAD"));

        var response = await client.GetAsync("/api/catalog-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<CatalogItemDto>>();
        Assert.Equal(["Pain", "Pommes"], items!.Select(item => item.Name));
    }

    [Fact]
    public async Task Create_ReturnsTheSavedItem()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/catalog-items", Request());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.Equal("Pain", item!.Name);
        Assert.Equal("Boulangerie", item.Category);
        Assert.Equal("BC-001", item.BusinessCentralCode);
        Assert.Equal("kg", item.Unit);
        Assert.Equal("A", item.QualityClass);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.NotEqual(default, item.CreatedAt);
        Assert.Null(item.UpdatedAt);
        Assert.Equal($"/api/catalog-items/{item.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Get_ReturnsTheItem()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "BC-GET"));

        var response = await client.GetAsync($"/api/catalog-items/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.Equal(created.Id, item!.Id);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync($"/api/catalog-items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReplacesTheFields()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "BC-EDIT"));

        var response = await client.PutAsJsonAsync(
            $"/api/catalog-items/{created.Id}",
            Request(name: "Pain complet", category: "Boulangerie", code: "BC-EDIT-2", unit: "caisse", qualityClass: "B"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.Equal("Pain complet", item!.Name);
        Assert.Equal("BC-EDIT-2", item.BusinessCentralCode);
        Assert.Equal("caisse", item.Unit);
        Assert.Equal("B", item.QualityClass);
        Assert.NotNull(item.UpdatedAt);
    }

    [Fact]
    public async Task Update_KeepsTheSameBusinessCentralCode()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "BC-SAME"));

        var response = await client.PutAsJsonAsync($"/api/catalog-items/{created.Id}", Request(name: "Pain bio", code: "BC-SAME"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        using var client = await CreateClientAsync();

        var response = await client.PutAsJsonAsync($"/api/catalog-items/{Guid.NewGuid()}", Request(code: "BC-MISSING"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheBusinessCentralCodeExists()
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(code: "BC-DUP"));

        var response = await client.PostAsJsonAsync("/api/catalog-items", Request(name: "Riz", code: "BC-DUP"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenTheBusinessCentralCodeBelongsToAnotherItem()
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(name: "Riz", code: "BC-RICE"));
        var bread = await CreateAsync(client, Request(code: "BC-BREAD-2"));

        var response = await client.PutAsJsonAsync($"/api/catalog-items/{bread.Id}", Request(code: "BC-RICE"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTheQualityClassDoesNotExist()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/catalog-items", Request(qualityClass: "Z", code: "BC-BAD"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(nameof(CatalogItemRequest.QualityClass), problem!.Errors.Keys);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    public async Task Create_ReturnsForbidden_WhenRoleCannotManageThePriceGrid(string role)
    {
        using var client = await CreateClientAsync(role);

        var response = await client.PostAsJsonAsync("/api/catalog-items", Request(code: $"BC-FORBIDDEN-{role}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_WhenRoleCannotManageThePriceGrid()
    {
        using var adminClient = await CreateClientAsync();
        var created = await CreateAsync(adminClient, Request(code: "BC-NO-EDIT"));
        using var client = await CreateClientAsync(RoleCodes.Supply);

        var response = await client.PutAsJsonAsync($"/api/catalog-items/{created.Id}", Request(name: "Pain bio", code: "BC-NO-EDIT"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOk_WhenRoleCannotManageThePriceGrid()
    {
        using var client = await CreateClientAsync(RoleCodes.Supply);

        var response = await client.GetAsync("/api/catalog-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTheNameIsBlank()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/catalog-items", Request(name: "  ", code: "BC-BLANK"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateClientAsync(string role = RoleCodes.Admin)
    {
        await factory.SignInAsAsync(role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static CatalogItemRequest Request(
        string name = "Pain",
        string category = "Boulangerie",
        string code = "BC-001",
        string unit = "kg",
        string qualityClass = "A") => new(name, category, code, unit, qualityClass);

    private static async Task<CatalogItemDto> CreateAsync(HttpClient client, CatalogItemRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/catalog-items", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CatalogItemDto>())!;
    }

    private async Task ClearAsync()
    {
        await using var context = new MoissonDbContext(
            new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(factory.ConnectionString).Options);
        await context.CatalogItems.ExecuteDeleteAsync();
    }
}
