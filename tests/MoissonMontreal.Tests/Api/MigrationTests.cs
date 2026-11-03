using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class MigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migrate_CreatesTheAppRoles_OnEmptyDatabase()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        var codes = await context.Roles.OrderBy(r => r.Id).Select(r => r.Code).ToListAsync();
        Assert.Equal(["Supply", "SupplyDirector", "Finance", "Admin"], codes);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenUserRoleDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.Users.Add(new User { Email = "unknown.role@moissonmontreal.org", DisplayName = "Unknown", RoleId = 99 });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}