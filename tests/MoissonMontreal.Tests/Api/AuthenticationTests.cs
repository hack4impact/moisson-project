using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

public class AuthenticationTests(AuthenticatedApiFactory factory) : IClassFixture<AuthenticatedApiFactory>
{
    [Fact]
    public async Task Me_ReturnsCurrentUser_WhenTokenIsValid()
    {
        var response = await GetMeAsync(factory.CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.Equal(
            new CurrentUserDto(
                "11111111-1111-1111-1111-111111111111",
                "Test Employee",
                "test.employee@moissonmontreal.org",
                factory.TenantId),
            user);
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
    public async Task Me_ReturnsUnauthorized_WhenIssuerIsWrong()
    {
        var response = await GetMeAsync(factory.CreateToken(issuer: "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0"));

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
