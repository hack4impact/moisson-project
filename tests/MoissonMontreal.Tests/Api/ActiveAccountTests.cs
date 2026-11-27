using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

// Routes without a specific permission use the default policy, which must still require an active account.
[Trait("Category", "Integration")]
public class ActiveAccountTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>
{
    private const string DefaultPolicyRoute = "/api/catalog-items";

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    [InlineData(RoleCodes.Admin)]
    public async Task DefaultRoute_IsAllowed_ForAnActiveAccountOfAnyRole(string role)
    {
        await factory.ReplaceUsersWithAsync(role);

        var response = await GetAsync(DefaultPolicyRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DefaultRoute_IsForbidden_WhenTheAccountIsDeactivated()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Admin, isActive: false);

        var response = await GetAsync(DefaultPolicyRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DefaultRoute_IsForbidden_WhenTheAccountWasNeverAdded()
    {
        await factory.RemoveAllUsersAsync();

        var response = await GetAsync(DefaultPolicyRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(DefaultPolicyRoute)]
    [InlineData("/api/me")]
    public async Task DeactivatedAndNeverAddedAccounts_GetTheSameResponse(string route)
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Admin, isActive: false);
        var deactivated = await GetAsync(route);

        await factory.RemoveAllUsersAsync();
        var neverAdded = await GetAsync(route);

        Assert.Equal(deactivated.StatusCode, neverAdded.StatusCode);
        Assert.Equal(await deactivated.Content.ReadAsStringAsync(), await neverAdded.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnAccountDeactivatedWhileItsTokenIsStillValid_IsRefusedOnItsNextRequest()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Supply);
        var token = factory.CreateToken();
        var before = await GetAsync(DefaultPolicyRoute, token);

        await using (var context = factory.CreateDbContext())
        {
            await context.Users.ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        }

        var after = await GetAsync(DefaultPolicyRoute, token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden), (before.StatusCode, after.StatusCode));
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? token = null)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? factory.CreateToken());
        return await client.GetAsync(path);
    }
}
