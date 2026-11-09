using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MoissonMontreal.Tests.Fixtures;

public class AuthenticatedApiFactory : WebApplicationFactory<Program>
{
    public SecurityKey SigningKey { get; } = new RsaSecurityKey(RSA.Create(2048));

    public string TenantId => AzureAd["TenantId"]!;

    public string Audience => AzureAd["Audience"]!;

    public string Issuer => IssuerFrom(AzureAd);

    private IConfigurationSection AzureAd => Services.GetRequiredService<IConfiguration>().GetSection("AzureAd");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Moisson", "Host=127.0.0.1;Port=1;Database=unused;Username=test");

        builder.ConfigureTestServices(services => services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IConfiguration>((options, configuration) =>
            {
                var issuer = IssuerFrom(configuration.GetSection("AzureAd"));
                var openIdConfiguration = new OpenIdConnectConfiguration { Issuer = issuer };
                openIdConfiguration.SigningKeys.Add(SigningKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(openIdConfiguration);
                options.TokenValidationParameters.IssuerValidator = null;
                options.TokenValidationParameters.ValidIssuer = issuer;
            }));
    }

    public string CreateToken(
        string? issuer = null,
        string? audience = null,
        string scope = "access_as_user",
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            IssuedAt = expiresAt.AddHours(-2),
            NotBefore = expiresAt.AddHours(-2),
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                ["oid"] = "11111111-1111-1111-1111-111111111111",
                ["tid"] = TenantId,
                ["name"] = "Test Employee",
                ["email"] = "test.employee@moissonmontreal.org",
                ["scp"] = scope,
            },
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }

    private static string IssuerFrom(IConfigurationSection azureAd) => $"{azureAd["Instance"]}{azureAd["TenantId"]}/v2.0";
}
