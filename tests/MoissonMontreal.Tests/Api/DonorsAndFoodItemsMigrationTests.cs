using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class DonorsAndFoodItemsMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migrate_CreatesDonorsAndFoodItems_OnEmptyDatabase()
    {
        await using var context = CreateContext("donors_food_items_empty");

        await context.Database.MigrateAsync();

        Assert.Equal(0, await context.Donors.CountAsync());
        Assert.Equal(0, await context.FoodItems.CountAsync());
    }

    [Fact]
    public async Task Donor_StoresName()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Donors.SingleAsync(d => d.Id == seed.DonorId);

        Assert.Equal("Boulangerie Saint-Laurent", saved.Name);
    }

    [Fact]
    public async Task Donor_StoresAddress()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Donors.SingleAsync(d => d.Id == seed.DonorId);

        Assert.Equal("123 rue Notre-Dame", saved.Address);
    }

    [Fact]
    public async Task Donor_StoresPostalCode()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Donors.SingleAsync(d => d.Id == seed.DonorId);

        Assert.Equal("H2X 1Y4", saved.PostalCode);
    }

    [Fact]
    public async Task Donor_StoresContactPerson()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Donors.SingleAsync(d => d.Id == seed.DonorId);

        Assert.Equal("Alex Martin", saved.ContactPerson);
    }

    [Fact]
    public async Task Donor_SetsCreatedAt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Donors.SingleAsync(d => d.Id == seed.DonorId);

        Assert.NotEqual(default, saved.CreatedAt);
    }

    [Fact]
    public async Task FoodItem_IsLinkedToItsDonor()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.Include(f => f.Donor).SingleAsync(f => f.Id == seed.ItemId);

        Assert.Equal(seed.DonorId, saved.DonorId);
        Assert.Equal(seed.DonorId, saved.Donor.Id);
    }

    [Fact]
    public async Task FoodItem_StoresName()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.SingleAsync(f => f.Id == seed.ItemId);

        Assert.Equal("Pain", saved.Name);
    }

    [Fact]
    public async Task FoodItem_StoresQuantity()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.SingleAsync(f => f.Id == seed.ItemId);

        Assert.Equal(12.50m, saved.Quantity);
    }

    [Fact]
    public async Task FoodItem_StoresUnit()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.SingleAsync(f => f.Id == seed.ItemId);

        Assert.Equal("kg", saved.Unit);
    }

    [Fact]
    public async Task FoodItem_StoresDonationDate()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.SingleAsync(f => f.Id == seed.ItemId);

        Assert.Equal(new DateOnly(2026, 11, 3), saved.DonationDate);
    }

    [Fact]
    public async Task FoodItem_StoresQuality()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.SingleAsync(f => f.Id == seed.ItemId);

        Assert.Equal("A", saved.Quality);
    }

    [Fact]
    public async Task FoodItem_SetsCreatedAt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.FoodItems.SingleAsync(f => f.Id == seed.ItemId);

        Assert.NotEqual(default, saved.CreatedAt);
    }

    [Fact]
    public async Task SaveFoodItem_Throws_WhenDonorDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.FoodItems.Add(new FoodItem
        {
            DonorId = Guid.NewGuid(),
            Name = "Riz",
            Quantity = 1m,
            Unit = "sac",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "B"
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_food_items_donors_donor_id");
    }

    [Fact]
    public async Task DeleteDonor_Throws_WhenFoodItemsRemain()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var donor = await context.Donors.SingleAsync(d => d.Id == seed.DonorId);
        context.Donors.Remove(donor);

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_food_items_donors_donor_id");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenQualityIsNotABandC()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.FoodItems.Add(new FoodItem
        {
            Donor = new Donor { Name = "Ferme des Îles" },
            Name = "Pommes",
            Quantity = 4m,
            Unit = "caisse",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "D"
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_food_items_quality");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenQuantityIsNotPositive()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.FoodItems.Add(new FoodItem
        {
            Donor = new Donor { Name = "Ferme des Îles" },
            Name = "Pommes",
            Quantity = -5m,
            Unit = "kg",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "A"
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_food_items_quantity_positive");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenDonorNameIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.Donors.Add(new Donor { Name = "" });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_donors_name_not_empty");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenFoodItemNameIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.FoodItems.Add(new FoodItem
        {
            Donor = new Donor { Name = "Ferme des Îles" },
            Name = "",
            Quantity = 1m,
            Unit = "kg",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "A"
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_food_items_name_not_empty");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenUnitIsEmpty()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.FoodItems.Add(new FoodItem
        {
            Donor = new Donor { Name = "Ferme des Îles" },
            Name = "Pommes",
            Quantity = 1m,
            Unit = "",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "A"
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_food_items_unit_not_empty");
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private async Task<Seed> SeedAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var donor = new Donor
        {
            Name = "Boulangerie Saint-Laurent",
            Address = "123 rue Notre-Dame",
            PostalCode = "H2X 1Y4",
            ContactPerson = "Alex Martin"
        };
        var item = new FoodItem
        {
            Donor = donor,
            Name = "Pain",
            Quantity = 12.50m,
            Unit = "kg",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "A"
        };

        context.FoodItems.Add(item);
        await context.SaveChangesAsync();

        return new Seed(donor.Id, item.Id);
    }

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

    private sealed record Seed(Guid DonorId, Guid ItemId);
}
