using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class PermissionTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>
{
    private const string ProtectedEndpoint = "/test/permissions/approve-receipts-level-1";

    private static readonly string[] AllPermissions =
    [
        Permissions.ManageDonations, Permissions.ManageDonors, Permissions.ManageReceipts,
        Permissions.ApproveReceiptsLevel1, Permissions.ApproveReceiptsLevel2,
        Permissions.ViewHistory, Permissions.ExportData,
        Permissions.ManageAccounts, Permissions.ManagePriceGrid, Permissions.ManageQualityPercentages,
    ];

    private static readonly Dictionary<string, string[]> PrdPermissionsByRole = new()
    {
        [RoleCodes.Supply] = [Permissions.ManageDonations, Permissions.ManageDonors, Permissions.ManageReceipts],
        [RoleCodes.SupplyDirector] =
        [
            Permissions.ManageDonations, Permissions.ManageDonors, Permissions.ManageReceipts,
            Permissions.ApproveReceiptsLevel1,
        ],
        [RoleCodes.Finance] = [Permissions.ApproveReceiptsLevel2, Permissions.ViewHistory, Permissions.ExportData],
        [RoleCodes.Admin] = AllPermissions,
    };

    public static TheoryData<string, string> RolesAndPermissions()
    {
        var data = new TheoryData<string, string>();
        foreach (var role in PrdPermissionsByRole.Keys)
        {
            foreach (var permission in AllPermissions)
            {
                data.Add(role, permission);
            }
        }

        return data;
    }

    [Fact]
    public async Task RoleCodes_MatchTheRolesInTheDatabase()
    {
        await using var context = factory.CreateDbContext();

        var codes = await context.Roles.OrderBy(r => r.Id).Select(r => r.Code).ToListAsync();

        Assert.Equal([RoleCodes.Supply, RoleCodes.SupplyDirector, RoleCodes.Finance, RoleCodes.Admin], codes);
    }

    [Theory]
    [MemberData(nameof(RolesAndPermissions))]
    public async Task Policy_FollowsThePrdTable(string role, string permission)
    {
        await factory.ReplaceUsersWithAsync(role);

        var allowed = await AuthorizeAsync(permission);

        Assert.Equal(PrdPermissionsByRole[role].Contains(permission), allowed);
    }

    [Fact]
    public async Task Policy_IgnoresRolesFromTheToken()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Supply);

        var allowed = await AuthorizeAsync(Permissions.ManageAccounts, tokenRole: RoleCodes.Admin);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Admin)]
    public async Task ProtectedEndpoint_ReturnsOk_WhenRoleIsAllowed(string role)
    {
        await factory.ReplaceUsersWithAsync(role);

        var response = await GetProtectedEndpointAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.Finance)]
    public async Task ProtectedEndpoint_ReturnsForbidden_WhenRoleIsNotAllowed(string role)
    {
        await factory.ReplaceUsersWithAsync(role);

        var response = await GetProtectedEndpointAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsForbidden_WhenUserHasNoAccount()
    {
        await factory.RemoveAllUsersAsync();

        var response = await GetProtectedEndpointAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsForbidden_WhenAccessWasRemoved()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Admin, isActive: false);

        var response = await GetProtectedEndpointAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.Admin);

        var response = await GetProtectedEndpointAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_LinksTheAccount_OnFirstSignIn()
    {
        await factory.ReplaceUsersWithAsync(RoleCodes.SupplyDirector, isLinked: false);

        var response = await GetProtectedEndpointAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = factory.CreateDbContext();
        var user = await context.Users.SingleAsync();
        Assert.Equal(Guid.Parse(AuthenticatedApiFactory.ObjectId), user.EntraObjectId);
    }

    private async Task<bool> AuthorizeAsync(string permission, string? tokenRole = null)
    {
        var claims = new List<Claim>
        {
            new("oid", AuthenticatedApiFactory.ObjectId),
            new("scp", "access_as_user"),
        };
        if (tokenRole is not null)
        {
            claims.Add(new Claim("roles", tokenRole));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(user, permission);
        return result.Succeeded;
    }

    private async Task<HttpResponseMessage> GetProtectedEndpointAsync(string? token)
    {
        using var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.GetAsync(ProtectedEndpoint);
    }
}
