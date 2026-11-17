using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
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
