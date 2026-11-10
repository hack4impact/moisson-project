using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Authorization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Moisson")
    ?? throw new InvalidOperationException(
        "La chaîne de connexion 'Moisson' est absente. Définissez ConnectionStrings__Moisson " +
        "ou utilisez 'dotnet user-secrets'.");

// Add services to the container.
builder.Services.AddDbContext<MoissonDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MoissonDbContext>("database");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options => options.MapInboundClaims = false);

var accessAsUserPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireScope("access_as_user")
    .Build();

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(accessAsUserPolicy)
    .SetFallbackPolicy(accessAsUserPolicy)
    .AddPermissionPolicies();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Health probes call /health over plain HTTP, so it must not be redirected.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseHttpsRedirection());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
