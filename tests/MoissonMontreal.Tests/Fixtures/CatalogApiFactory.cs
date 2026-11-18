using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using Testcontainers.PostgreSql;

namespace MoissonMontreal.Tests.Fixtures;

/// <summary>
/// API host signed in with a test token and backed by a disposable PostgreSQL database.
/// </summary>
public sealed class CatalogApiFactory : AuthenticatedApiFactory, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(ConnectionString).Options;
        await using var context = new MoissonDbContext(options);
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Makes the signed-in test employee the only user, with the given role.
    /// </summary>
    public async Task SignInAsAsync(string roleCode)
    {
        await using var context = new MoissonDbContext(
            new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(ConnectionString).Options);
        await context.Users.ExecuteDeleteAsync();
        var roleId = await context.Roles.Where(r => r.Code == roleCode).Select(r => r.Id).SingleAsync();
        context.Users.Add(new User
        {
            Email = Email,
            DisplayName = Name,
            RoleId = roleId,
            IsActive = true,
            EntraObjectId = Guid.Parse(ObjectId),
        });
        await context.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Moisson", ConnectionString);
    }
}
