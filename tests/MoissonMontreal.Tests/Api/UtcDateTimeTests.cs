using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class UtcDateTimeTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public void Model_ConvertsEveryDateTimeOffsetToUtc()
    {
        using var context = CreateContext();

        var properties = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?))
            .ToList();

        Assert.NotEmpty(properties);
        Assert.All(properties, p => Assert.IsType<UtcDateTimeOffsetConverter>(p.GetValueConverter()));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-12)]
    [InlineData(14)]
    public async Task User_SavesCreatedAtAndUpdatedAt_WhenGivenWithAnOffset(int offsetHours)
    {
        var moment = NewYearsEve(offsetHours);
        int userId;

        await using (var saveContext = await MigratedContextAsync())
        {
            var user = new User
            {
                Email = $"offset{offsetHours + 100}@moissonmontreal.org",
                DisplayName = "Offset",
                RoleId = 1,
                CreatedAt = moment,
                UpdatedAt = moment.AddHours(1)
            };
            saveContext.Users.Add(user);
            await saveContext.SaveChangesAsync();
            userId = user.Id;
        }

        await using var context = CreateContext();
        var saved = await context.Users.SingleAsync(u => u.Id == userId);

        AssertSameMomentInUtc(moment, saved.CreatedAt);
        Assert.NotNull(saved.UpdatedAt);
        AssertSameMomentInUtc(moment.AddHours(1), saved.UpdatedAt.Value);
    }

    [Fact]
    public async Task User_KeepsUpdatedAtEmpty_WhenNeverChanged()
    {
        int userId;

        await using (var saveContext = await MigratedContextAsync())
        {
            var user = new User { Email = "never.changed@moissonmontreal.org", DisplayName = "Never changed", RoleId = 1 };
            saveContext.Users.Add(user);
            await saveContext.SaveChangesAsync();
            userId = user.Id;
        }

        await using var context = CreateContext();
        var saved = await context.Users.SingleAsync(u => u.Id == userId);

        Assert.Null(saved.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, saved.CreatedAt.Offset);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(14)]
    public async Task DonorAndFoodItem_SaveCreatedAt_WhenGivenWithAnOffset(int offsetHours)
    {
        var moment = NewYearsEve(offsetHours);
        Guid donorId;
        Guid foodItemId;

        await using (var saveContext = await MigratedContextAsync())
        {
            var donor = new Donor { Name = "Fruiterie Mont-Royal", CreatedAt = moment };
            var foodItem = new FoodItem
            {
                Donor = donor,
                Name = "Oranges",
                Quantity = 4m,
                Unit = "caisse",
                DonationDate = new DateOnly(2026, 12, 31),
                Quality = "A",
                CreatedAt = moment
            };
            saveContext.FoodItems.Add(foodItem);
            await saveContext.SaveChangesAsync();
            donorId = donor.Id;
            foodItemId = foodItem.Id;
        }

        await using var context = CreateContext();
        var savedDonor = await context.Donors.SingleAsync(d => d.Id == donorId);
        var savedFoodItem = await context.FoodItems.SingleAsync(f => f.Id == foodItemId);

        AssertSameMomentInUtc(moment, savedDonor.CreatedAt);
        AssertSameMomentInUtc(moment, savedFoodItem.CreatedAt);
        Assert.Equal(new DateOnly(2026, 12, 31), savedFoodItem.DonationDate);
    }

    private static DateTimeOffset NewYearsEve(int offsetHours) =>
        new(2026, 12, 31, 20, 0, 0, TimeSpan.FromHours(offsetHours));

    private static void AssertSameMomentInUtc(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.Equal(expected.UtcDateTime, actual.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
    }

    private async Task<MoissonDbContext> MigratedContextAsync()
    {
        var context = CreateContext();
        await context.Database.MigrateAsync();
        return context;
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}
