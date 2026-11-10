using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Api;

namespace MoissonMontreal.Tests.Fixtures;

public class PermissionsApiFactory : AuthenticatedApiFactory, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Moisson", _postgres.ConnectionString);
        builder.ConfigureTestServices(services => services
            .AddControllers()
            .AddApplicationPart(typeof(PermissionTestController).Assembly));
    }

    public MoissonDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    public async Task ReplaceUsersWithAsync(string roleCode, bool isActive = true, bool isLinked = true)
    {
        await using var context = CreateDbContext();
        await context.Users.ExecuteDeleteAsync();
        var roleId = await context.Roles.Where(r => r.Code == roleCode).Select(r => r.Id).SingleAsync();
        context.Users.Add(new User
        {
            Email = Email,
            DisplayName = Name,
            RoleId = roleId,
            IsActive = isActive,
            EntraObjectId = isLinked ? Guid.Parse(ObjectId) : null,
        });
        await context.SaveChangesAsync();
    }

    public async Task RemoveAllUsersAsync()
    {
        await using var context = CreateDbContext();
        await context.Users.ExecuteDeleteAsync();
    }
}
