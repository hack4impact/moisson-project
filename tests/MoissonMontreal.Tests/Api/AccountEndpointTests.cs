using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class AccountEndpointTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>
{
    private const string Url = "/api/accounts";

    public static TheoryData<string, string, string> NonAdminRolesAndRoutes()
    {
        var data = new TheoryData<string, string, string>();
        string[] roles = [RoleCodes.Supply, RoleCodes.SupplyDirector, RoleCodes.Finance];
        (string Method, string Path)[] routes =
        [
            ("GET", Url),
            ("GET", $"{Url}/roles"),
            ("GET", $"{Url}/1"),
            ("GET", $"{Url}/1/history"),
            ("POST", Url),
            ("PUT", $"{Url}/1"),
            ("PUT", $"{Url}/1/role"),
            ("POST", $"{Url}/1/deactivate"),
            ("POST", $"{Url}/1/reactivate"),
        ];
        foreach (var role in roles)
        {
            foreach (var (method, path) in routes)
            {
                data.Add(role, method, path);
            }
        }

        return data;
    }

    [Fact]
    public async Task List_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(NonAdminRolesAndRoutes))]
    public async Task EveryRoute_IsForbidden_ForANonAdministrator(string role, string method, string path)
    {
        using var client = await SignInAsync(role);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : JsonContent.Create(new { }),
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListRoles_ReturnsTheFourRoles()
    {
        using var client = await SignInAsync();

        var roles = await client.GetFromJsonAsync<List<RoleDto>>($"{Url}/roles");

        Assert.Equal(RoleCodes.All, roles!.Select(r => r.Code));
    }

    [Fact]
    public async Task Create_ReturnsTheAccount_AndTheEmployeeCanThenSignIn()
    {
        using var admin = await SignInAsync();

        var response = await admin.PostAsJsonAsync(Url, new AccountCreateRequest("New.Employee@MoissonMontreal.org", "New Employee", RoleCodes.Supply));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal($"{Url}/{account.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(("new.employee@moissonmontreal.org", "New Employee", RoleCodes.Supply, true, false),
            (account.Email, account.DisplayName, account.RoleCode, account.IsActive, account.IsLinked));

        using var employee = ClientFor(Guid.NewGuid(), "new.employee@moissonmontreal.org");
        var me = await employee.GetFromJsonAsync<CurrentUserDto>("/api/me");
        Assert.Equal(RoleCodes.Supply, me!.Role);

        var linked = await admin.GetFromJsonAsync<AccountDto>($"{Url}/{account.Id}");
        Assert.True(linked!.IsLinked);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheEmailIsUsedRegardlessOfCase()
    {
        using var admin = await SignInAsync();
        await CreateAsync(admin, "taken@moissonmontreal.org");

        var response = await admin.PostAsJsonAsync(Url, new AccountCreateRequest("TAKEN@MoissonMontreal.org", "Other", RoleCodes.Finance));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var admin = await SignInAsync();

        var response = await admin.PostAsJsonAsync(Url, new AccountCreateRequest(" ", null, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email", body);
        Assert.Contains("DisplayName", body);
        Assert.Contains("RoleCode", body);
    }

    [Theory]
    [InlineData("no-at-sign.org", RoleCodes.Supply, "Email")]
    [InlineData("jean@moissonmontreal.org", "Volunteer", "RoleCode")]
    public async Task Create_ReturnsBadRequest_ForAnInvalidEmailOrAnUnknownRole(string email, string role, string field)
    {
        using var admin = await SignInAsync();

        var response = await admin.PostAsJsonAsync(Url, new AccountCreateRequest(email, "Jean", role));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenAnotherAdminChangedTheAccountInTheMeantime()
    {
        using var admin = await SignInAsync();
        var account = await CreateAsync(admin, "edit@moissonmontreal.org");

        var first = await admin.PutAsJsonAsync($"{Url}/{account.Id}", new AccountUpdateRequest(account.Email, "First Edit", account.Version));
        var second = await admin.PutAsJsonAsync($"{Url}/{account.Id}", new AccountUpdateRequest(account.Email, "Second Edit", account.Version));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict), (first.StatusCode, second.StatusCode));
        Assert.Equal("First Edit", (await admin.GetFromJsonAsync<AccountDto>($"{Url}/{account.Id}"))!.DisplayName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTheVersionIsMissing()
    {
        using var admin = await SignInAsync();
        var account = await CreateAsync(admin, "noversion@moissonmontreal.org");

        var response = await admin.PutAsJsonAsync($"{Url}/{account.Id}", new AccountUpdateRequest(account.Email, "Name", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Version", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenTheAccountDoesNotExist()
    {
        using var admin = await SignInAsync();

        var response = await admin.GetAsync($"{Url}/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_And_Deactivate_ReturnBadRequest_ForTheAdministratorsOwnAccount()
    {
        using var admin = await SignInAsync();
        var self = (await admin.GetFromJsonAsync<List<AccountDto>>(Url))!.Single(a => a.Email == AuthenticatedApiFactory.Email);

        var role = await admin.PutAsJsonAsync($"{Url}/{self.Id}/role", new AccountRoleRequest(RoleCodes.Supply, self.Version));
        var deactivate = await admin.PostAsJsonAsync($"{Url}/{self.Id}/deactivate", new AccountVersionRequest(self.Version));

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (role.StatusCode, deactivate.StatusCode));
    }

    [Fact]
    public async Task ChangeRole_TakesEffect_AndTheHistoryShowsTheRoleCodes()
    {
        using var admin = await SignInAsync();
        var account = await CreateAsync(admin, "role@moissonmontreal.org");

        var response = await admin.PutAsJsonAsync($"{Url}/{account.Id}/role", new AccountRoleRequest(RoleCodes.Finance, account.Version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(RoleCodes.Finance, (await response.Content.ReadFromJsonAsync<AccountDto>())!.RoleCode);
        var history = (await admin.GetFromJsonAsync<List<AccountChangeDto>>($"{Url}/{account.Id}/history"))!;
        Assert.Equal(["RoleChanged", "Created"], history.Select(c => c.Action));
        Assert.Equal((RoleCodes.Supply, RoleCodes.Finance), (history[0].OldRoleCode, history[0].NewRoleCode));
        Assert.All(history, c => Assert.Equal(AuthenticatedApiFactory.Name, c.ChangedByName));
    }

    [Fact]
    public async Task Deactivate_RefusesTheEmployeeOnTheirNextRequest_AndReactivateGivesTheAccessBack()
    {
        using var admin = await SignInAsync();
        var account = await CreateAsync(admin, "leaving@moissonmontreal.org");
        using var employee = ClientFor(Guid.NewGuid(), "leaving@moissonmontreal.org");
        var beforeStatus = (await employee.GetAsync("/api/me")).StatusCode;
        var linked = (await admin.GetFromJsonAsync<AccountDto>($"{Url}/{account.Id}"))!;

        var deactivate = await admin.PostAsJsonAsync($"{Url}/{account.Id}/deactivate", new AccountVersionRequest(linked.Version));
        var deactivated = (await deactivate.Content.ReadFromJsonAsync<AccountDto>())!;
        var duringStatus = (await employee.GetAsync("/api/me")).StatusCode;

        var reactivate = await admin.PostAsJsonAsync($"{Url}/{account.Id}/reactivate", new AccountVersionRequest(deactivated.Version));
        var afterStatus = (await employee.GetAsync("/api/me")).StatusCode;

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (deactivate.StatusCode, reactivate.StatusCode));
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.OK), (beforeStatus, duringStatus, afterStatus));

        var list = await admin.GetFromJsonAsync<List<AccountDto>>(Url);
        Assert.Contains(list!, a => a.Id == account.Id && a.IsActive);
        var history = await admin.GetFromJsonAsync<List<AccountChangeDto>>($"{Url}/{account.Id}/history");
        Assert.Equal(["Reactivated", "Deactivated", "Created"], history!.Select(c => c.Action));
    }

    [Fact]
    public async Task List_ShowsActiveAccountsFirst_ThenDeactivatedOnes()
    {
        using var admin = await SignInAsync();
        var leaving = await CreateAsync(admin, "a.leaving@moissonmontreal.org");
        await CreateAsync(admin, "z.staying@moissonmontreal.org");
        await admin.PostAsJsonAsync($"{Url}/{leaving.Id}/deactivate", new AccountVersionRequest(leaving.Version));

        var list = await admin.GetFromJsonAsync<List<AccountDto>>(Url);

        Assert.Equal(list!.OrderByDescending(a => a.IsActive).Select(a => a.Id), list!.Select(a => a.Id));
        Assert.False(list!.Last().IsActive);
    }

    [Fact]
    public async Task History_ReturnsNotFound_WhenTheAccountDoesNotExist()
    {
        using var admin = await SignInAsync();

        var response = await admin.GetAsync($"{Url}/999999/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheAccountDoesNotExist()
    {
        using var admin = await SignInAsync();

        var response = await admin.PutAsJsonAsync($"{Url}/999999", new AccountUpdateRequest("ghost@moissonmontreal.org", "Ghost", 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "role")]
    [InlineData("POST", "deactivate")]
    [InlineData("POST", "reactivate")]
    public async Task Changes_ReturnBadRequest_WhenTheVersionIsMissing(string method, string action)
    {
        using var admin = await SignInAsync();
        var account = await CreateAsync(admin, $"{action}.noversion@moissonmontreal.org");
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{Url}/{account.Id}/{action}")
        {
            Content = JsonContent.Create(new { roleCode = RoleCodes.Finance }),
        };

        var response = await admin.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Version", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DataCreatedByAnEmployee_StaysAttributedToThem_AfterDeactivationAndReactivation()
    {
        using var admin = await SignInAsync();
        var account = await CreateAsync(admin, "attributed@moissonmontreal.org");
        Guid catalogItemId;
        await using (var context = factory.CreateDbContext())
        {
            var item = TestCatalogItems.Create("Poivron");
            context.CatalogItems.Add(item);
            await context.SaveChangesAsync();
            catalogItemId = item.Id;
        }

        using (var employee = ClientFor(Guid.NewGuid(), "attributed@moissonmontreal.org"))
        {
            var mapping = await employee.PostAsJsonAsync("/api/bc-item-mappings", new BcItemMappingCreateRequest("ATTRIBUTED-1", catalogItemId));
            Assert.Equal(HttpStatusCode.Created, mapping.StatusCode);
        }

        var linked = (await admin.GetFromJsonAsync<AccountDto>($"{Url}/{account.Id}"))!;
        var deactivated = await admin.PostAsJsonAsync($"{Url}/{account.Id}/deactivate", new AccountVersionRequest(linked.Version));
        var reactivate = await admin.PostAsJsonAsync(
            $"{Url}/{account.Id}/reactivate",
            new AccountVersionRequest((await deactivated.Content.ReadFromJsonAsync<AccountDto>())!.Version));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        var saved = await admin.GetFromJsonAsync<BcItemMappingDto>("/api/bc-item-mappings/ATTRIBUTED-1");
        Assert.Equal((account.Id, account.DisplayName), (saved!.ConfirmedByUserId, saved.ConfirmedByName));
    }

    // ReplaceUsersWithAsync deletes every user, and the history and the BC mappings point to users, so they are emptied first.
    private async Task<HttpClient> SignInAsync(string role = RoleCodes.Admin)
    {
        await using (var context = factory.CreateDbContext())
        {
            await context.UserAccountChanges.ExecuteDeleteAsync();
            await context.BcItemMappingChanges.ExecuteDeleteAsync();
            await context.BcItemMappings.ExecuteDeleteAsync();
        }

        await factory.ReplaceUsersWithAsync(role);
        return ClientFor(Guid.Parse(AuthenticatedApiFactory.ObjectId), AuthenticatedApiFactory.Email);
    }

    private HttpClient ClientFor(Guid objectId, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.CreateToken(email: email, objectId: objectId.ToString()));
        return client;
    }

    private static async Task<AccountDto> CreateAsync(HttpClient admin, string email)
    {
        var response = await admin.PostAsJsonAsync(Url, new AccountCreateRequest(email, "Employee " + email, RoleCodes.Supply));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountDto>())!;
    }
}
