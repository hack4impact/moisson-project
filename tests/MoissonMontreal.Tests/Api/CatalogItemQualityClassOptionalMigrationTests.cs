using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class CatalogItemQualityClassOptionalMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string MigrationBeforeOptionalQualityClass = "20261117040905_AddCatalogItemDetails";

    [Fact]
    public async Task Migrate_KeepsTheQualityClassOfExistingItems()
    {
        await using var context = CreateContext("class_optional_upgrade");
        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeOptionalQualityClass);
        await InsertItemAsync(context, "00000000-0000-0000-0000-0000000000e1", "BC-UPGRADE", "B");

        await context.Database.MigrateAsync();

        var item = await context.CatalogItems.SingleAsync(i => i.BusinessCentralCode == "BC-UPGRADE");
        Assert.Equal("B", item.QualityClassCode);
    }

    [Fact]
    public async Task SaveChanges_StoresAnItemWithoutQualityClass()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var item = TestCatalogItems.Create("Poivron");
        item.QualityClassCode = null;
        context.CatalogItems.Add(item);

        await context.SaveChangesAsync();

        await using var reread = CreateContext();
        var saved = await reread.CatalogItems.Include(i => i.QualityClass).SingleAsync(i => i.Id == item.Id);
        Assert.Null(saved.QualityClassCode);
        Assert.Null(saved.QualityClass);
    }

    [Fact]
    public async Task SaveChanges_StillRefusesAQualityClassThatDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var item = TestCatalogItems.Create("Poivron");
        item.QualityClassCode = "Z";
        context.CatalogItems.Add(item);

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, CatalogItemConfiguration.QualityClassForeignKey);
    }

    [Fact]
    public async Task RollingBack_StopsWithAClearError_AndChangesNothing_WhenAnItemHasNoClass()
    {
        const string database = "class_optional_rollback_refused";
        await using (var setup = CreateContext(database))
        {
            await setup.Database.MigrateAsync();
            await InsertItemAsync(setup, "00000000-0000-0000-0000-0000000000e2", "BC-NO-CLASS", null);
        }

        await using (var rollbackContext = CreateContext(database))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => rollbackContext.GetService<IMigrator>().MigrateAsync(MigrationBeforeOptionalQualityClass));
            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("some catalog items have no quality class", exception.MessageText);
        }

        await using var context = CreateContext(database);
        Assert.EndsWith("_MakeCatalogItemQualityClassOptional", (await context.Database.GetAppliedMigrationsAsync()).Last());
        var qualityClass = await context.CatalogItems
            .Where(i => i.BusinessCentralCode == "BC-NO-CLASS")
            .Select(i => i.QualityClassCode)
            .SingleAsync();
        Assert.Null(qualityClass);
    }

    [Fact]
    public async Task RollingBack_MakesTheClassRequiredAgain_WhenEveryItemHasAClass()
    {
        await using var context = CreateContext("class_optional_rollback");
        await context.Database.MigrateAsync();
        await InsertItemAsync(context, "00000000-0000-0000-0000-0000000000e3", "BC-WITH-CLASS", "A");

        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeOptionalQualityClass);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertItemAsync(context, "00000000-0000-0000-0000-0000000000e4", "BC-AFTER-ROLLBACK", null));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, exception.SqlState);
    }

    private static Task<int> InsertItemAsync(MoissonDbContext context, string id, string code, string? qualityClass) =>
        context.Database.ExecuteSqlAsync($"""
            INSERT INTO catalog_items (id, name, category, business_central_code, unit, quality_class)
            VALUES ({Guid.Parse(id)}, 'Poivron', 'To classify', {code}, 'CA', {qualityClass});
            """);

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
