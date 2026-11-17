using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class CatalogItemsMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CatalogItem_StoresCategoryBusinessCentralCodeUnitAndQualityClass()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.CatalogItems.Include(item => item.QualityClass).SingleAsync(item => item.Id == seed.Id);

        Assert.Equal("Boulangerie", saved.Category);
        Assert.Equal(seed.Code, saved.BusinessCentralCode);
        Assert.Equal("kg", saved.Unit);
        Assert.Equal("A", saved.QualityClassCode);
        Assert.Equal("A", saved.QualityClass.Code);
        Assert.Null(saved.UpdatedAt);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenBusinessCentralCodeIsDuplicated()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.CatalogItems.Add(Item(code: seed.Code, name: "Pain blanc"));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, CatalogItemConfiguration.BusinessCentralCodeIndex);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenCategoryIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.CatalogItems.Add(Item(category: ""));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_catalog_items_category_not_empty");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenBusinessCentralCodeIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.CatalogItems.Add(Item(code: ""));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_catalog_items_business_central_code_not_empty");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenUnitIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.CatalogItems.Add(Item(unit: ""));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_catalog_items_unit_not_empty");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenQualityClassDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.CatalogItems.Add(Item(qualityClass: "Z"));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_catalog_items_quality_classes_quality_class");
    }

    [Fact]
    public async Task DeleteQualityClass_Throws_WhenACatalogItemUsesIt()
    {
        await SeedAsync();

        await using var context = CreateContext();
        var quality = await context.QualityClasses.SingleAsync(q => q.Code == "A");
        context.QualityClasses.Remove(quality);

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_catalog_items_quality_classes_quality_class");
    }

    private async Task<Seed> SeedAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var code = "BC-" + Guid.NewGuid().ToString("N")[..12];
        var item = Item(code: code);
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return new Seed(item.Id, code);
    }

    private static CatalogItem Item(
        string name = "Pain",
        string category = "Boulangerie",
        string code = "BC-PAIN",
        string unit = "kg",
        string qualityClass = "A") => new()
    {
        Name = name,
        Category = category,
        BusinessCentralCode = code,
        Unit = unit,
        QualityClassCode = qualityClass,
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

    private sealed record Seed(Guid Id, string Code);
}
