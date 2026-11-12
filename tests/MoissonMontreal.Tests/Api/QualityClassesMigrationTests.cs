using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class QualityClassesMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string MigrationBeforeQualityClasses = "20261110175320_CreateCatalogItemsAndPrices";

    [Fact]
    public async Task Migrate_CreatesTheThreeQualityClasses_OnEmptyDatabase()
    {
        await using var context = CreateContext("quality_empty");

        await context.Database.MigrateAsync();

        Assert.Equal(["A", "B", "C"], await context.QualityClasses.OrderBy(q => q.Code).Select(q => q.Code).ToListAsync());
    }

    [Theory]
    [InlineData("A", "Grocery quality", "100", "100", "100")]
    [InlineData("B", "Imperfect", "50", "50", "75")]
    [InlineData("C", "Near expiry", "0", "0", "0")]
    public async Task QualityClass_HasANameAndAPercentage(string code, string name, string percentage, string min, string max)
    {
        await using var context = await MigratedContextAsync();

        var saved = await context.QualityClasses.SingleAsync(q => q.Code == code);

        Assert.Equal(name, saved.Name);
        Assert.Equal(Decimal(percentage), saved.Percentage);
        Assert.Equal(Decimal(min), saved.MinPercentage);
        Assert.Equal(Decimal(max), saved.MaxPercentage);
    }

    [Fact]
    public async Task Percentages_AreStoredAsExactDecimals_NotFloats()
    {
        await MigratedContextAsync();
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT column_name, data_type, numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_name = 'quality_classes' AND column_name LIKE '%percentage'
            ORDER BY column_name
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add($"{reader.GetString(0)} {reader.GetString(1)}({reader.GetInt32(2)},{reader.GetInt32(3)})");
        }

        Assert.Equal(["max_percentage numeric(5,2)", "min_percentage numeric(5,2)", "percentage numeric(5,2)"], columns);
    }

    [Theory]
    [InlineData("D", "62.50", "62.50")]
    [InlineData("E", "66.67", "66.67")]
    [InlineData("F", "66.665", "66.67")]
    [InlineData("G", "0.01", "0.01")]
    [InlineData("H", "99.99", "99.99")]
    public async Task Percentage_KeepsTwoDecimals_RoundingHalfAwayFromZero(string code, string entered, string stored)
    {
        await using (var saveContext = await MigratedContextAsync())
        {
            saveContext.QualityClasses.Add(new QualityClass { Code = code, Name = $"Test {code}", Percentage = Decimal(entered), MinPercentage = 0m, MaxPercentage = 100m });
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var saved = await context.QualityClasses.SingleAsync(q => q.Code == code);

        Assert.Equal(Decimal(stored), saved.Percentage);
    }

    [Fact]
    public async Task FoodItem_IsLinkedToItsQualityClass()
    {
        Guid foodItemId;
        await using (var saveContext = await MigratedContextAsync())
        {
            var foodItem = NewFoodItem("B");
            saveContext.FoodItems.Add(foodItem);
            await saveContext.SaveChangesAsync();
            foodItemId = foodItem.Id;
        }

        await using var context = CreateContext();
        var saved = await context.FoodItems.Include(f => f.QualityClass).SingleAsync(f => f.Id == foodItemId);

        Assert.Equal("B", saved.QualityClass.Code);
        Assert.Equal(50m, saved.QualityClass.Percentage);
        Assert.Equal((50m, 75m), (saved.QualityClass.MinPercentage, saved.QualityClass.MaxPercentage));
    }

    [Fact]
    public async Task FoodItem_CanUseANewQualityClass()
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = "S", Name = "Sorted", Percentage = 80m, MinPercentage = 80m, MaxPercentage = 90m });
        context.FoodItems.Add(NewFoodItem("S"));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveFoodItem_Throws_WhenQualityClassDoesNotExist()
    {
        await using var context = await MigratedContextAsync();
        context.FoodItems.Add(NewFoodItem("Z"));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_food_items_quality_classes_quality");
    }

    [Fact]
    public async Task DeleteQualityClass_Throws_WhenFoodItemsUseIt()
    {
        await using (var saveContext = await MigratedContextAsync())
        {
            saveContext.FoodItems.Add(NewFoodItem("C"));
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        context.QualityClasses.Remove(await context.QualityClasses.SingleAsync(q => q.Code == "C"));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_food_items_quality_classes_quality");
    }

    [Theory]
    [InlineData("I", "49.99")]
    [InlineData("J", "75.01")]
    public async Task SaveChanges_Throws_WhenPercentageIsOutsideItsRange(string code, string percentage)
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = code, Name = $"Test {code}", Percentage = Decimal(percentage), MinPercentage = 50m, MaxPercentage = 75m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_quality_classes_percentage_in_range");
    }

    [Theory]
    [InlineData("K", "-0.01", "0", "100")]
    [InlineData("L", "0", "50", "100.01")]
    public async Task SaveChanges_Throws_WhenRangeGoesBeyond0To100(string code, string min, string percentage, string max)
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = code, Name = $"Test {code}", Percentage = Decimal(percentage), MinPercentage = Decimal(min), MaxPercentage = Decimal(max) });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_quality_classes_range_valid");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenMinIsAboveMax()
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = "M", Name = "Test M", Percentage = 75m, MinPercentage = 80m, MaxPercentage = 70m });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Contains(postgresException.ConstraintName, new[] { "ck_quality_classes_percentage_in_range", "ck_quality_classes_range_valid" });
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenPercentageDoesNotFitFiveDigits()
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = "N", Name = "Test N", Percentage = 1000m, MinPercentage = 0m, MaxPercentage = 1000m });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.NumericValueOutOfRange, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenNameIsAlreadyUsed()
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = "O", Name = "Imperfect", Percentage = 60m, MinPercentage = 50m, MaxPercentage = 75m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ix_quality_classes_name");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenCodeIsAlreadyUsed()
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = "A", Name = "Another A", Percentage = 100m, MinPercentage = 100m, MaxPercentage = 100m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "pk_quality_classes");
    }

    [Theory]
    [InlineData("", "Empty code", "ck_quality_classes_code_format")]
    [InlineData("P", "", "ck_quality_classes_name_not_empty")]
    public async Task SaveChanges_Throws_WhenCodeOrNameIsEmpty(string code, string name, string constraint)
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = code, Name = name, Percentage = 50m, MinPercentage = 0m, MaxPercentage = 100m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, constraint);
    }

    [Theory]
    [InlineData("b")]
    [InlineData("z")]
    [InlineData("1")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("É")]
    public async Task SaveChanges_Throws_WhenCodeIsNotOneCapitalLetter(string code)
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = code, Name = $"Code {code}", Percentage = 50m, MinPercentage = 0m, MaxPercentage = 100m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_quality_classes_code_format");
    }

    [Fact]
    public async Task SaveChanges_AcceptsZ_TheLastCapitalLetter()
    {
        await using var context = CreateContext("quality_code_z");
        await context.Database.MigrateAsync();
        context.QualityClasses.Add(new QualityClass { Code = "Z", Name = "Last letter", Percentage = 50m, MinPercentage = 0m, MaxPercentage = 100m });

        await context.SaveChangesAsync();
    }

    [Fact]
    public void InitialClasses_AreInsertedByTheMigration_NotManagedByTheModel()
    {
        using var context = CreateContext();

        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.Empty(model.FindEntityType(typeof(QualityClass))!.GetSeedData());
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenCodeIsLongerThanOneLetter()
    {
        await using var context = await MigratedContextAsync();
        context.QualityClasses.Add(new QualityClass { Code = "AB", Name = "Two letters", Percentage = 50m, MinPercentage = 0m, MaxPercentage = 100m });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Migrate_KeepsExistingFoodItems_AndLinksThemToTheirClass()
    {
        await using var context = CreateContext("quality_upgrade");
        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeQualityClasses);
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO donors (id, name) VALUES ('00000000-0000-0000-0000-0000000000d1', 'Boulangerie Saint-Laurent');
            INSERT INTO food_items (donor_id, name, quantity, unit, donation_date, quality) VALUES
              ('00000000-0000-0000-0000-0000000000d1', 'Pain', 12.50, 'kg', '2026-11-03', 'A'),
              ('00000000-0000-0000-0000-0000000000d1', 'Pommes', 3, 'caisse', '2026-11-03', 'B'),
              ('00000000-0000-0000-0000-0000000000d1', 'Yogourt', 2, 'kg', '2026-11-03', 'C');
            """);

        await context.Database.MigrateAsync();

        var percentages = await context.FoodItems.OrderBy(f => f.Quality).Select(f => f.QualityClass.Percentage).ToListAsync();
        Assert.Equal([100m, 50m, 0m], percentages);
    }

    [Fact]
    public async Task RollingBack_RemovesQualityClasses_AndRestoresTheGradeCheck()
    {
        await using var context = CreateContext("quality_rollback");
        await context.Database.MigrateAsync();

        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeQualityClasses);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            INSERT INTO donors (id, name) VALUES ('00000000-0000-0000-0000-0000000000d2', 'Ferme des Îles');
            INSERT INTO food_items (donor_id, name, quantity, unit, donation_date, quality)
            VALUES ('00000000-0000-0000-0000-0000000000d2', 'Pommes', 4, 'caisse', '2026-11-03', 'D');
            """));
        Assert.Equal("ck_food_items_quality", exception.ConstraintName);
    }

    [Fact]
    public async Task RollingBack_KeepsFoodItemsGradedAToC()
    {
        await using (var saveContext = CreateContext("quality_rollback_abc"))
        {
            await saveContext.Database.MigrateAsync();
            saveContext.FoodItems.AddRange(NewFoodItem("A"), NewFoodItem("B"), NewFoodItem("C"));
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext("quality_rollback_abc");
        await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeQualityClasses);

        Assert.Equal(["A", "B", "C"], await context.Database.SqlQueryRaw<string>("SELECT quality AS \"Value\" FROM food_items ORDER BY quality").ToListAsync());
        Assert.Equal(MigrationBeforeQualityClasses, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    [Fact]
    public async Task RollingBack_Stops_WhenAFoodItemUsesANewClass()
    {
        const string database = "quality_rollback_new_class";
        await using (var saveContext = CreateContext(database))
        {
            await saveContext.Database.MigrateAsync();
            saveContext.QualityClasses.Add(new QualityClass { Code = "S", Name = "Sorted", Percentage = 80m, MinPercentage = 80m, MaxPercentage = 90m });
            saveContext.FoodItems.Add(NewFoodItem("S"));
            await saveContext.SaveChangesAsync();
        }

        await AssertRollbackStopsAsync(database, "some food items use a quality class other than A, B or C");

        await using var context = CreateContext(database);
        Assert.Equal("S", (await context.FoodItems.Include(f => f.QualityClass).SingleAsync()).QualityClass.Code);
    }

    [Theory]
    [InlineData("quality_rollback_added", "INSERT INTO quality_classes VALUES ('S', 'Sorted', 80, 80, 90)")]
    [InlineData("quality_rollback_percentage", "UPDATE quality_classes SET percentage = 60 WHERE code = 'B'")]
    [InlineData("quality_rollback_range", "UPDATE quality_classes SET max_percentage = 70 WHERE code = 'B'")]
    [InlineData("quality_rollback_name", "UPDATE quality_classes SET name = 'Imparfait' WHERE code = 'B'")]
    public async Task RollingBack_Stops_WhenAQualityClassWasAddedOrChanged(string database, string change)
    {
        await using (var saveContext = CreateContext(database))
        {
            await saveContext.Database.MigrateAsync();
            await saveContext.Database.ExecuteSqlRawAsync(change);
        }

        await AssertRollbackStopsAsync(database, "quality classes were added or changed since the migration");
    }

    private async Task AssertRollbackStopsAsync(string database, string expectedMessage)
    {
        await using (var rollbackContext = CreateContext(database))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => rollbackContext.GetService<IMigrator>().MigrateAsync(MigrationBeforeQualityClasses));
            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains(expectedMessage, exception.MessageText);
        }

        await using var context = CreateContext(database);
        Assert.EndsWith("_CreateQualityClasses", (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.True(await context.QualityClasses.AnyAsync());
    }

    private static FoodItem NewFoodItem(string quality) => new()
    {
        Donor = new Donor { Name = "Ferme des Îles" },
        Name = "Pommes",
        Quantity = 4m,
        Unit = "caisse",
        DonationDate = new DateOnly(2026, 11, 3),
        Quality = quality
    };

    private static decimal Decimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private async Task<MoissonDbContext> MigratedContextAsync()
    {
        var context = CreateContext();
        await context.Database.MigrateAsync();
        return context;
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
}
