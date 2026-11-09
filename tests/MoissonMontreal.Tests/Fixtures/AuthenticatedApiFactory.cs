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
using Microsoft.IdentityModel.Validators;

namespace MoissonMontreal.Tests.Fixtures;

public class AuthenticatedApiFactory : WebApplicationFactory<Program>
{
    public const string ObjectId = "11111111-1111-1111-1111-111111111111";
    public const string Name = "Test Employee";
    public const string Email = "test.employee@moissonmontreal.org";
    public const string PreferredUsername = "temployee@moissonmontreal.org";

    public SecurityKey SigningKey { get; } = new RsaSecurityKey(RSA.Create(2048));

    public string TenantId => AzureAd["TenantId"]!;

    public string Audience => AzureAd["Audience"]!;

    private IConfigurationSection AzureAd => Services.GetRequiredService<IConfiguration>().GetSection("AzureAd");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Moisson", "Host=127.0.0.1;Port=1;Database=unused;Username=test");

        builder.ConfigureTestServices(services => services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IConfiguration>((options, configuration) =>
            {
                var azureAd = configuration.GetSection("AzureAd");
                var metadata = new OpenIdConnectConfiguration
                {
                    Issuer = IssuerFor(azureAd, PublishedIssuerTenant(azureAd["TenantId"]!)),
                };
                metadata.SigningKeys.Add(SigningKey);
                var metadataManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                options.ConfigurationManager = metadataManager;
                options.TokenValidationParameters.IssuerValidator = AadIssuerValidator
                    .GetAadIssuerValidator(options.Authority!, new HttpClient(), _ => metadataManager)
                    .Validate;
            }));
    }

    public string CreateToken(
        string? tenantId = null,
        string? audience = null,
        string scope = "access_as_user",
        DateTime? expires = null,
        SecurityKey? signingKey = null,
        string? email = Email)
    {
        var tokenTenantId = tenantId ?? TenantId;
        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);
        var claims = new Dictionary<string, object>
        {
            ["oid"] = ObjectId,
            ["tid"] = tokenTenantId,
            ["name"] = Name,
            ["preferred_username"] = PreferredUsername,
            ["scp"] = scope,
        };
        if (email is not null)
        {
            claims["email"] = email;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = IssuerFor(AzureAd, tokenTenantId),
            Audience = audience ?? Audience,
            IssuedAt = expiresAt.AddHours(-2),
            NotBefore = expiresAt.AddHours(-2),
            Expires = expiresAt,
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }

    private static string IssuerFor(IConfigurationSection azureAd, string tenantId) => $"{azureAd["Instance"]}{tenantId}/v2.0";

    private static string PublishedIssuerTenant(string configuredTenantId) =>
        Guid.TryParse(configuredTenantId, out _) ? configuredTenantId : "{tenantid}";
}
