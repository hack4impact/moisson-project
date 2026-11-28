using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class UserCreatedByMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string MigrationBeforeCreatedBy = "20261120031640_MakeCatalogItemQualityClassOptional";

    [Fact]
    public async Task Migrate_KeepsExistingAccounts_WithNoCreator()
    {
        await using var context = CreateContext("user_created_by_upgrade");
        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeCreatedBy);
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (email, display_name, role_id) VALUES
              ('first.admin@moissonmontreal.org', 'First Admin', 4),
              ('marie@moissonmontreal.org', 'Marie Tremblay', 3);
            """);

        await context.Database.MigrateAsync();

        await using var reader = CreateContext("user_created_by_upgrade");
        var users = await reader.Users.OrderBy(u => u.Email).ToListAsync();
        Assert.Equal(["first.admin@moissonmontreal.org", "marie@moissonmontreal.org"], users.Select(u => u.Email));
        Assert.All(users, user => Assert.Null(user.CreatedById));
    }

    [Fact]
    public async Task SaveChanges_StoresTheAdminWhoCreatedTheAccount()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var admin = Account("admin");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var created = Account("created");
        created.CreatedById = admin.Id;
        context.Users.Add(created);
        await context.SaveChangesAsync();

        await using var reader = CreateContext();
        var saved = await reader.Users.Include(u => u.CreatedBy).SingleAsync(u => u.Id == created.Id);

        Assert.Equal(admin.Id, saved.CreatedById);
        Assert.Equal(admin.Email, saved.CreatedBy!.Email);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenTheCreatorDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var user = Account("no.creator");
        user.CreatedById = int.MaxValue;
        context.Users.Add(user);

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_users_users_created_by_id");
    }

    [Fact]
    public async Task Delete_Throws_WhenTheAccountCreatedAnotherAccount()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var admin = Account("creator");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var created = Account("created.by.creator");
        created.CreatedById = admin.Id;
        context.Users.Add(created);
        await context.SaveChangesAsync();

        await using var deleter = CreateContext();
        deleter.Users.Remove(await deleter.Users.SingleAsync(u => u.Id == admin.Id));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(deleter, "fk_users_users_created_by_id");
    }

    [Fact]
    public async Task Migrate_IndexesTheCreator()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var definition = await context.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'ix_users_created_by_id'")
            .SingleAsync();

        Assert.Equal("CREATE INDEX ix_users_created_by_id ON public.users USING btree (created_by_id)", definition);
    }

    [Fact]
    public async Task RollingBack_RemovesTheCreatorColumn_AndKeepsTheAccounts()
    {
        await using var context = CreateContext("user_created_by_rollback");
        await context.Database.MigrateAsync();
        var admin = Account("rollback.admin");
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        var created = Account("rollback.created");
        created.CreatedById = admin.Id;
        context.Users.Add(created);
        await context.SaveChangesAsync();

        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeCreatedBy);

        var columns = await context.Database
            .SqlQueryRaw<string>("SELECT column_name::text AS \"Value\" FROM information_schema.columns WHERE table_name = 'users' ORDER BY column_name")
            .ToListAsync();
        Assert.Equal(["created_at", "display_name", "email", "entra_object_id", "id", "is_active", "role_id", "updated_at"], columns);
        var emails = await context.Database
            .SqlQueryRaw<string>("SELECT email AS \"Value\" FROM users ORDER BY email")
            .ToListAsync();
        Assert.Equal(["rollback.admin@moissonmontreal.org", "rollback.created@moissonmontreal.org"], emails);

        await context.Database.MigrateAsync();
        await using var reader = CreateContext("user_created_by_rollback");
        Assert.All(await reader.Users.ToListAsync(), user => Assert.Null(user.CreatedById));
    }

    private static User Account(string name) => new()
    {
        Email = $"{name}@moissonmontreal.org",
        DisplayName = name,
        RoleId = 4,
    };

    private MoissonDbContext CreateContext(string? database = null)
    {
        var connectionString = postgres.ConnectionString;
        if (database is not null)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            builder["Database"] = database;
            connectionString = builder.ConnectionString;
        }

        return new MoissonDbContext(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
    }
}
