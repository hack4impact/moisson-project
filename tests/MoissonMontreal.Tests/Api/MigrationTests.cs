using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

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

        await AssertViolatesConstraintAsync(context, "fk_users_roles_role_id");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenEmailHasUppercase()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.Users.Add(new User { Email = "Upper.Case@moissonmontreal.org", DisplayName = "Upper", RoleId = 1 });

        await AssertViolatesConstraintAsync(context, "ck_users_email_lowercase");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenEmailIsAlreadyUsed()
    {
        await using (var seedContext = CreateContext())
        {
            await seedContext.Database.MigrateAsync();
            seedContext.Users.Add(new User { Email = "duplicate@moissonmontreal.org", DisplayName = "First", RoleId = 1 });
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        context.Users.Add(new User { Email = "duplicate@moissonmontreal.org", DisplayName = "Second", RoleId = 1 });

        await AssertViolatesConstraintAsync(context, "ix_users_email");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenDeletingRoleInUse()
    {
        await using (var seedContext = CreateContext())
        {
            await seedContext.Database.MigrateAsync();
            seedContext.Users.Add(new User { Email = "role.in.use@moissonmontreal.org", DisplayName = "Finance", RoleId = 3 });
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        context.Roles.Remove(await context.Roles.SingleAsync(r => r.Id == 3));

        await AssertViolatesConstraintAsync(context, "fk_users_roles_role_id");
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);

    private static async Task AssertViolatesConstraintAsync(MoissonDbContext context, string constraintName)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(constraintName, postgresException.ConstraintName);
    }
}