using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

// Every route now looks up the employee's account, so these tests need a real database.
[Trait("Category", "Integration")]
public class AuthenticationTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>
{
    [Fact]
    public async Task Me_ReturnsCurrentUser_WhenTokenIsValid()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Supply);

        var response = await GetMeAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.Equal(
            new CurrentUserDto(
                AuthenticatedApiFactory.ObjectId,
                AuthenticatedApiFactory.Name,
                AuthenticatedApiFactory.Email,
                factory.TenantId,
                RoleCodes.Supply),
            user);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    [InlineData(RoleCodes.Admin)]
    public async Task Me_ReturnsTheRoleOfTheAccount(string role)
    {
        await factory.ReplaceUsersWithAsync(role);

        var user = await (await GetMeAsync(factory.CreateToken())).Content.ReadFromJsonAsync<CurrentUserDto>();

        Assert.Equal(role, user?.Role);
    }

    [Fact]
    public async Task Me_ReturnsTheNewRole_OnTheNextRequestAfterARoleChange()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Supply);
        var token = factory.CreateToken();
        var before = await (await GetMeAsync(token)).Content.ReadFromJsonAsync<CurrentUserDto>();

        await using (var context = factory.CreateDbContext())
        {
            var adminRoleId = await context.Roles.Where(r => r.Code == RoleCodes.Admin).Select(r => r.Id).SingleAsync();
            await context.Users.ExecuteUpdateAsync(set => set.SetProperty(u => u.RoleId, adminRoleId));
        }

        var after = await (await GetMeAsync(token)).Content.ReadFromJsonAsync<CurrentUserDto>();

        Assert.Equal((RoleCodes.Supply, RoleCodes.Admin), (before?.Role, after?.Role));
    }

    [Fact]
    public async Task Me_ReturnsPreferredUsernameAsEmail_WhenEmailClaimIsMissing()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Supply);

        var response = await GetMeAsync(factory.CreateToken(email: null));

        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.Equal(AuthenticatedApiFactory.PreferredUsername, user?.Email);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var response = await GetMeAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenTokenIsMalformed()
    {
        var response = await GetMeAsync("not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenTokenIsExpired()
    {
        var response = await GetMeAsync(factory.CreateToken(expires: DateTime.UtcNow.AddHours(-1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenSignatureIsInvalid()
    {
        var otherKey = new RsaSecurityKey(RSA.Create(2048));

        var response = await GetMeAsync(factory.CreateToken(signingKey: otherKey));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenTokenComesFromAnotherTenant()
    {
        var response = await GetMeAsync(factory.CreateToken(tenantId: "00000000-0000-0000-0000-000000000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenAudienceIsWrong()
    {
        var response = await GetMeAsync(factory.CreateToken(audience: "00000000-0000-0000-0000-000000000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsForbidden_WhenScopeIsMissing()
    {
        var response = await GetMeAsync(factory.CreateToken(scope: "other_scope"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DefaultPolicy_RequiresAccessAsUserScope()
    {
        var policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var defaultPolicy = await policies.GetDefaultPolicyAsync();

        Assert.Contains(
            defaultPolicy.Requirements.OfType<ScopeAuthorizationRequirement>(),
            requirement => requirement.AllowedValues?.Contains("access_as_user") == true);
    }

    private async Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        using var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.GetAsync("/api/me");
    }
}
