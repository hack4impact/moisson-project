using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class DonorEndpointTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task Get_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/donors/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/donors", Request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsTheSavedDonor()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/donors", Request(
            type: DonorTypes.Individual,
            name: " Marie Tremblay ",
            contactPerson: "Marie Tremblay",
            email: "marie@exemple.ca",
            phone: "514-555-0100",
            notes: "Donne chaque automne",
            code: "C-CREATE"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var donor = await response.Content.ReadFromJsonAsync<DonorDto>();
        Assert.NotEqual(Guid.Empty, donor!.Id);
        Assert.Equal("Marie Tremblay", donor.Name);
        Assert.Equal(DonorTypes.Individual, donor.Type);
        Assert.Equal("123 rue Saint-Laurent", donor.Address);
        Assert.Equal("Montréal", donor.City);
        Assert.Equal("QC", donor.Province);
        Assert.Equal("H2X 1Y4", donor.PostalCode);
        Assert.Equal("Marie Tremblay", donor.ContactPerson);
        Assert.Equal("marie@exemple.ca", donor.Email);
        Assert.Equal("514-555-0100", donor.Phone);
        Assert.Equal("Donne chaque automne", donor.Notes);
        Assert.Equal("C-CREATE", donor.BusinessCentralCode);
        Assert.NotEqual(default, donor.CreatedAt);
        Assert.Equal(TimeSpan.Zero, donor.CreatedAt.Offset);
        Assert.Null(donor.UpdatedAt);
        Assert.Equal($"/api/donors/{donor.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Get_ReturnsTheDonor()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "C-GET"));

        var response = await client.GetAsync($"/api/donors/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var donor = await response.Content.ReadFromJsonAsync<DonorDto>();
        Assert.Equal(created, donor);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenTheDonorDoesNotExist()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync($"/api/donors/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenTheIdIsNotAGuid()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync("/api/donors/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReplacesTheFields()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "C-EDIT", email: "old@exemple.ca", notes: "Ancienne note"));

        var response = await client.PutAsJsonAsync($"/api/donors/{created.Id}", Request(
            name: "Boulangerie Saint-Denis",
            type: DonorTypes.Organization,
            address: "456 rue Saint-Denis",
            city: "Laval",
            province: "Québec",
            postalCode: "H7N 2Z3",
            contactPerson: "Luc Gagnon",
            email: "",
            phone: "450-555-0199",
            notes: null,
            code: "C-EDIT-2"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var donor = await response.Content.ReadFromJsonAsync<DonorDto>();
        Assert.Equal(created.Id, donor!.Id);
        Assert.Equal("Boulangerie Saint-Denis", donor.Name);
        Assert.Equal("456 rue Saint-Denis", donor.Address);
        Assert.Equal("Laval", donor.City);
        Assert.Equal("Québec", donor.Province);
        Assert.Equal("H7N 2Z3", donor.PostalCode);
        Assert.Equal("Luc Gagnon", donor.ContactPerson);
        Assert.Null(donor.Email);
        Assert.Equal("450-555-0199", donor.Phone);
        Assert.Null(donor.Notes);
        Assert.Equal("C-EDIT-2", donor.BusinessCentralCode);
        Assert.Equal(created.CreatedAt, donor.CreatedAt);
        Assert.NotNull(donor.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, donor.UpdatedAt.Value.Offset);

        var saved = await client.GetFromJsonAsync<DonorDto>($"/api/donors/{created.Id}");
        Assert.Equal(donor with { UpdatedAt = null }, saved! with { UpdatedAt = null });
        Assert.NotNull(saved.UpdatedAt);
        Assert.True((donor.UpdatedAt.Value - saved.UpdatedAt.Value).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Update_CanChangeTheType()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(type: DonorTypes.Organization));

        var donor = await UpdateAsync(client, created.Id, Request(type: DonorTypes.Individual));

        Assert.Equal(DonorTypes.Individual, donor.Type);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheDonorDoesNotExist()
    {
        using var client = await CreateClientAsync();

        var response = await client.PutAsJsonAsync($"/api/donors/{Guid.NewGuid()}", Request());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheDonorIsDeletedDuringTheEdit()
    {
        using var adminClient = await CreateClientAsync();
        var created = await CreateAsync(adminClient, Request(code: null));
        var interceptor = new DonorDeletedMeanwhile(factory.ConnectionString, created.Id);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<MoissonDbContext>(options => options.AddInterceptors(interceptor))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());

        var response = await client.PutAsJsonAsync($"/api/donors/{created.Id}", Request(name: "Nom modifié", code: null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_KeepsTheDateOfTheLastChange_WhenNoFieldChanges()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: null));

        var unchanged = await UpdateAsync(client, created.Id, Request(name: $"  {created.Name}  ", code: null));
        Assert.Null(unchanged.UpdatedAt);

        var changed = await UpdateAsync(client, created.Id, Request(name: "Nom modifié", code: null));
        var changedAgain = await UpdateAsync(client, created.Id, Request(name: "Nom modifié", code: null));

        Assert.NotNull(changed.UpdatedAt);
        var saved = await client.GetFromJsonAsync<DonorDto>($"/api/donors/{created.Id}");
        Assert.Equal(saved!.UpdatedAt, changedAgain.UpdatedAt);
        Assert.True((changed.UpdatedAt.Value - saved.UpdatedAt!.Value).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WithOneMessagePerMissingField()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/donors",
            new DonorRequest(null, null, null, null, null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(
            ["Address", "City", "Name", "PostalCode", "Province", "Type"],
            problem!.Errors.Keys.Order());
        Assert.All(problem.Errors.Values, messages => Assert.Single(messages));
        Assert.Equal(["Name is required."], problem.Errors["Name"]);
        Assert.Equal(["Type must be individual or organization."], problem.Errors["Type"]);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_AndKeepsTheDonor_WhenAFieldIsInvalid()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request());

        var response = await client.PutAsJsonAsync($"/api/donors/{created.Id}", Request(name: "  ", email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["Email", "Name"], problem!.Errors.Keys.Order());
        var saved = await client.GetFromJsonAsync<DonorDto>($"/api/donors/{created.Id}");
        Assert.Equal(created, saved);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTheBodyIsMissing()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsync("/api/donors", JsonContent.Create<DonorRequest?>(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheBusinessCentralCodeExists()
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(code: "C-DUP"));

        var response = await client.PostAsJsonAsync("/api/donors", Request(name: "Épicerie Fine", code: "C-DUP"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Business Central code already used", problem!.Title);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheBusinessCentralCodeOnlyDiffersBySpaces()
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(code: "C-TRIM"));

        var response = await client.PostAsJsonAsync("/api/donors", Request(code: "  C-TRIM  "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_AcceptsTheSameBusinessCentralCode_WithDifferentCapitalLetters()
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(code: "C-CASE"));

        var response = await client.PostAsJsonAsync("/api/donors", Request(code: "c-case"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_AcceptsManyDonorsWithoutBusinessCentralCode()
    {
        using var client = await CreateClientAsync();

        var first = await CreateAsync(client, Request(code: null));
        var second = await CreateAsync(client, Request(code: "   "));

        Assert.Null(first.BusinessCentralCode);
        Assert.Null(second.BusinessCentralCode);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenTheBusinessCentralCodeBelongsToAnotherDonor()
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(code: "C-TAKEN"));
        var other = await CreateAsync(client, Request(code: "C-OTHER"));

        var response = await client.PutAsJsonAsync($"/api/donors/{other.Id}", Request(code: "C-TAKEN"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var saved = await client.GetFromJsonAsync<DonorDto>($"/api/donors/{other.Id}");
        Assert.Equal("C-OTHER", saved!.BusinessCentralCode);
    }

    [Fact]
    public async Task Update_KeepsTheSameBusinessCentralCode()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "C-SAME"));

        var response = await client.PutAsJsonAsync($"/api/donors/{created.Id}", Request(name: "Nouveau nom", code: "C-SAME"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_CanRemoveTheBusinessCentralCode()
    {
        using var client = await CreateClientAsync();
        var created = await CreateAsync(client, Request(code: "C-REMOVE"));

        var donor = await UpdateAsync(client, created.Id, Request(code: null));

        Assert.Null(donor.BusinessCentralCode);
        await CreateAsync(client, Request(code: "C-REMOVE"));
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Admin)]
    public async Task CreateAndUpdate_AreAllowed_WhenRoleCanManageDonors(string role)
    {
        using var client = await CreateClientAsync(role);

        var created = await CreateAsync(client, Request(code: null));
        var updated = await UpdateAsync(client, created.Id, Request(name: "Nom modifié", code: null));

        Assert.Equal("Nom modifié", updated.Name);
    }

    [Fact]
    public async Task Create_ReturnsForbidden_WhenRoleCannotManageDonors()
    {
        using var client = await CreateClientAsync(RoleCodes.Finance);

        var response = await client.PostAsJsonAsync("/api/donors", Request(code: null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_AndKeepsTheDonor_WhenRoleCannotManageDonors()
    {
        using var adminClient = await CreateClientAsync();
        var created = await CreateAsync(adminClient, Request(code: null));
        using var client = await CreateClientAsync(RoleCodes.Finance);

        var response = await client.PutAsJsonAsync($"/api/donors/{created.Id}", Request(name: "Nom modifié", code: null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var saved = await client.GetFromJsonAsync<DonorDto>($"/api/donors/{created.Id}");
        Assert.Equal(created.Name, saved!.Name);
    }

    [Fact]
    public async Task Get_ReturnsOk_WhenRoleCannotManageDonors()
    {
        using var adminClient = await CreateClientAsync();
        var created = await CreateAsync(adminClient, Request(code: null));
        using var client = await CreateClientAsync(RoleCodes.Finance);

        var response = await client.GetAsync($"/api/donors/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> CreateClientAsync(string role = RoleCodes.Admin)
    {
        await factory.SignInAsAsync(role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static DonorRequest Request(
        string? name = "Boulangerie Saint-Laurent",
        string? type = DonorTypes.Organization,
        string? address = "123 rue Saint-Laurent",
        string? city = "Montréal",
        string? province = "QC",
        string? postalCode = "H2X 1Y4",
        string? contactPerson = null,
        string? email = null,
        string? phone = null,
        string? notes = null,
        string? code = null) =>
        new(name, type, address, city, province, postalCode, contactPerson, email, phone, notes, code);

    private static async Task<DonorDto> CreateAsync(HttpClient client, DonorRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/donors", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DonorDto>())!;
    }

    private static async Task<DonorDto> UpdateAsync(HttpClient client, Guid id, DonorRequest request)
    {
        var response = await client.PutAsJsonAsync($"/api/donors/{id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DonorDto>())!;
    }

    /// <summary>
    /// Simulates another request that deletes the donor after the edit read it: just before the save, it deletes the donor through another connection, once.
    /// </summary>
    private sealed class DonorDeletedMeanwhile(string connectionString, Guid donorId) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = new MoissonDbContext(
                    new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
                await other.Donors.Where(donor => donor.Id == donorId).ExecuteDeleteAsync(cancellationToken);
            }

            return result;
        }
    }
}
