using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class ReceiptsMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migrate_CreatesReceiptsAndReceiptItems_OnEmptyDatabase()
    {
        await using var context = CreateContext("receipts_empty");

        await context.Database.MigrateAsync();

        Assert.Equal(0, await context.Receipts.CountAsync());
        Assert.Equal(0, await context.ReceiptItems.CountAsync());
    }

    [Fact]
    public async Task Receipt_IsLinkedToItsDonor()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Receipts.Include(r => r.Donor).SingleAsync(r => r.Id == seed.ReceiptId);

        Assert.Equal(seed.DonorId, saved.DonorId);
        Assert.Equal(seed.DonorId, saved.Donor.Id);
    }

    [Fact]
    public async Task Receipt_IsLinkedToItsFoodItems()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var foodItemIds = await context.ReceiptItems
            .Where(i => i.ReceiptId == seed.ReceiptId)
            .Select(i => i.FoodItem.Id)
            .ToListAsync();

        Assert.Equal(new[] { seed.BreadId, seed.ApplesId }.Order(), foodItemIds.Order());
    }

    [Fact]
    public async Task Receipt_StoresTotalAmount()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Receipts.SingleAsync(r => r.Id == seed.ReceiptId);

        Assert.Equal(44.97m, saved.TotalAmount);
    }

    [Fact]
    public async Task Receipt_SetsCreatedAt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Receipts.SingleAsync(r => r.Id == seed.ReceiptId);

        Assert.NotEqual(default, saved.CreatedAt);
    }

    [Fact]
    public async Task Receipt_SetsReceiptDate_InMontrealTime()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.Receipts.SingleAsync(r => r.Id == seed.ReceiptId);
        var montreal = TimeZoneInfo.FindSystemTimeZoneById("America/Montreal");

        Assert.Equal(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(saved.CreatedAt, montreal).DateTime), saved.ReceiptDate);
    }

    [Fact]
    public async Task Receipt_SavesCreatedAt_WhenGivenInMontrealTime()
    {
        var seed = await SeedAsync();
        var newYearsEve = new DateTimeOffset(2026, 12, 31, 20, 0, 0, TimeSpan.FromHours(-5));
        Guid receiptId;

        await using (var saveContext = CreateContext())
        {
            var receipt = new Receipt { DonorId = seed.DonorId, TotalAmount = 0m, CreatedAt = newYearsEve };
            saveContext.Receipts.Add(receipt);
            await saveContext.SaveChangesAsync();
            receiptId = receipt.Id;
        }

        await using var context = CreateContext();
        var saved = await context.Receipts.SingleAsync(r => r.Id == receiptId);

        Assert.Equal(newYearsEve, saved.CreatedAt);
        Assert.Equal(TimeSpan.Zero, saved.CreatedAt.Offset);
    }

    [Fact]
    public async Task ReceiptItem_StoresUnitPriceWithFourDecimals()
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using (var saveContext = CreateContext())
        {
            saveContext.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, foodItemId, unitPrice: 0.125m));
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var saved = await context.ReceiptItems.SingleAsync(i => i.FoodItemId == foodItemId);

        Assert.Equal(0.125m, saved.UnitPrice);
    }

    [Fact]
    public async Task ReceiptItem_StoresQualityPercent()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.ReceiptItems.SingleAsync(i => i.ReceiptId == seed.ReceiptId && i.FoodItemId == seed.BreadId);

        Assert.Equal(75m, saved.QualityPercent);
    }

    [Fact]
    public async Task ReceiptItem_KeepsItsQuantityAndUnit_WhenTheFoodItemChanges()
    {
        var seed = await SeedAsync();

        await using (var editContext = CreateContext())
        {
            var bread = await editContext.FoodItems.SingleAsync(f => f.Id == seed.BreadId);
            bread.Quantity = 20m;
            bread.Unit = "caisse";
            await editContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var saved = await context.ReceiptItems.SingleAsync(i => i.ReceiptId == seed.ReceiptId && i.FoodItemId == seed.BreadId);

        Assert.Equal(12.50m, saved.Quantity);
        Assert.Equal("kg", saved.Unit);
    }

    [Fact]
    public async Task ReceiptItems_LineValuesAddUpToTotalAmount()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var lineValues = await context.ReceiptItems
            .Where(i => i.ReceiptId == seed.ReceiptId)
            .OrderBy(i => i.FoodItem.Name)
            .Select(i => Math.Round(i.Quantity * i.UnitPrice * i.QualityPercent / 100m, 2))
            .ToListAsync();
        var total = await context.Receipts.Where(r => r.Id == seed.ReceiptId).Select(r => r.TotalAmount).SingleAsync();

        Assert.Equal([37.50m, 7.47m], lineValues);
        Assert.Equal(total, lineValues.Sum());
    }

    [Fact]
    public async Task ReceiptItem_LineValueRoundsHalfAwayFromZero()
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using (var saveContext = CreateContext())
        {
            saveContext.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, foodItemId, unitPrice: 0.45m, qualityPercent: 50m));
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var lineValue = await context.ReceiptItems
            .Where(i => i.FoodItemId == foodItemId)
            .Select(i => Math.Round(i.Quantity * i.UnitPrice * i.QualityPercent / 100m, 2))
            .SingleAsync();

        Assert.Equal(0.23m, lineValue);
        Assert.Equal(Math.Round(0.225m, 2, MidpointRounding.AwayFromZero), lineValue);
    }

    [Fact]
    public async Task ReceiptItem_AcceptsZeroPercent_ForQualityC()
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using var context = CreateContext();
        context.Receipts.Add(new Receipt
        {
            DonorId = seed.DonorId,
            TotalAmount = 0m,
            Items = [Item(Guid.Empty, seed.DonorId, foodItemId, unitPrice: 3.00m, qualityPercent: 0m)]
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteReceipt_DeletesItsItems_ButKeepsTheFoodItems()
    {
        var seed = await SeedAsync();

        await using (var deleteContext = CreateContext())
        {
            deleteContext.Receipts.Remove(await deleteContext.Receipts.SingleAsync(r => r.Id == seed.ReceiptId));
            await deleteContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        Assert.Equal(0, await context.ReceiptItems.CountAsync(i => i.ReceiptId == seed.ReceiptId));
        Assert.Equal(2, await context.FoodItems.CountAsync(f => f.Id == seed.BreadId || f.Id == seed.ApplesId));
    }

    [Fact]
    public async Task SaveReceipt_Throws_WhenDonorDoesNotExist()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        context.Receipts.Add(new Receipt { DonorId = Guid.NewGuid(), TotalAmount = 10m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipts_donors_donor_id");
    }

    [Fact]
    public async Task SaveReceiptItem_Throws_WhenFoodItemDoesNotExist()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, Guid.NewGuid()));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_items_food_items_food_item_id_donor_id");
    }

    [Fact]
    public async Task SaveReceiptItem_Throws_WhenFoodItemIsAlreadyOnTheReceipt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, seed.BreadId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "pk_receipt_items");
    }

    [Fact]
    public async Task SaveReceiptItem_Throws_WhenFoodItemIsOnAnotherReceipt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.Receipts.Add(new Receipt
        {
            DonorId = seed.DonorId,
            TotalAmount = 37.50m,
            Items = [Item(Guid.Empty, seed.DonorId, seed.BreadId, quantity: 12.50m, qualityPercent: 75m)]
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ix_receipt_items_food_item_id");
    }

    [Fact]
    public async Task SaveReceiptItem_Throws_WhenFoodItemBelongsToAnotherDonor()
    {
        var seed = await SeedAsync();
        var otherDonorId = await AddDonorAsync();
        var otherFoodItemId = await AddFoodItemAsync(otherDonorId);

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, otherFoodItemId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_items_food_items_food_item_id_donor_id");
    }

    [Fact]
    public async Task SaveReceiptItem_Throws_WhenReceiptBelongsToAnotherDonor()
    {
        var seed = await SeedAsync();
        var otherDonorId = await AddDonorAsync();
        var otherFoodItemId = await AddFoodItemAsync(otherDonorId);

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, otherDonorId, otherFoodItemId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_items_receipts_receipt_id_donor_id");
    }

    [Fact]
    public async Task DeleteDonor_Throws_WhenReceiptsRemain()
    {
        Guid donorId;
        await using (var seedContext = CreateContext())
        {
            await seedContext.Database.MigrateAsync();
            var donor = new Donor { Name = "Épicerie du Plateau" };
            seedContext.Receipts.Add(new Receipt { Donor = donor, TotalAmount = 0m });
            await seedContext.SaveChangesAsync();
            donorId = donor.Id;
        }

        await using var context = CreateContext();
        context.Donors.Remove(await context.Donors.SingleAsync(d => d.Id == donorId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipts_donors_donor_id");
    }

    [Fact]
    public async Task DeleteFoodItem_Throws_WhenItIsOnAReceipt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.FoodItems.Remove(await context.FoodItems.SingleAsync(f => f.Id == seed.BreadId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_items_food_items_food_item_id_donor_id");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenTotalAmountIsNegative()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.Receipts.Add(new Receipt { DonorId = seed.DonorId, TotalAmount = -1m });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipts_total_amount_not_negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveChanges_Throws_WhenQuantityIsNotPositive(int quantity)
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, foodItemId, quantity: quantity));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_items_quantity_positive");
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenUnitIsEmpty()
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, foodItemId, unit: ""));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_items_unit_not_empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveChanges_Throws_WhenUnitPriceIsNotPositive(int unitPrice)
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, foodItemId, unitPrice: unitPrice));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_items_unit_price_positive");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SaveChanges_Throws_WhenQualityPercentIsOutOfRange(int qualityPercent)
    {
        var seed = await SeedAsync();
        var foodItemId = await AddFoodItemAsync(seed.DonorId);

        await using var context = CreateContext();
        context.ReceiptItems.Add(Item(seed.ReceiptId, seed.DonorId, foodItemId, qualityPercent: qualityPercent));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_items_quality_percent_range");
    }

    private async Task<Seed> SeedAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var donor = new Donor { Name = "Boulangerie Saint-Laurent" };
        var bread = new FoodItem
        {
            Donor = donor,
            Name = "Pain",
            Quantity = 12.50m,
            Unit = "kg",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "B"
        };
        var apples = new FoodItem
        {
            Donor = donor,
            Name = "Pommes",
            Quantity = 3m,
            Unit = "caisse",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "A"
        };
        var receipt = new Receipt
        {
            Donor = donor,
            TotalAmount = 44.97m,
            Items =
            [
                new ReceiptItem { FoodItem = bread, Quantity = bread.Quantity, Unit = bread.Unit, UnitPrice = 4.00m, QualityPercent = 75m },
                new ReceiptItem { FoodItem = apples, Quantity = apples.Quantity, Unit = apples.Unit, UnitPrice = 2.49m, QualityPercent = 100m }
            ]
        };

        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        return new Seed(donor.Id, receipt.Id, bread.Id, apples.Id);
    }

    private async Task<Guid> AddDonorAsync()
    {
        await using var context = CreateContext();
        var donor = new Donor { Name = "Marché Jean-Talon" };
        context.Donors.Add(donor);
        await context.SaveChangesAsync();

        return donor.Id;
    }

    private async Task<Guid> AddFoodItemAsync(Guid donorId)
    {
        await using var context = CreateContext();
        var foodItem = new FoodItem
        {
            DonorId = donorId,
            Name = "Yogourt",
            Quantity = 1m,
            Unit = "kg",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "B"
        };
        context.FoodItems.Add(foodItem);
        await context.SaveChangesAsync();

        return foodItem.Id;
    }

    private static ReceiptItem Item(
        Guid receiptId,
        Guid donorId,
        Guid foodItemId,
        decimal quantity = 1m,
        string unit = "kg",
        decimal unitPrice = 4.00m,
        decimal qualityPercent = 100m) => new()
    {
        ReceiptId = receiptId,
        DonorId = donorId,
        FoodItemId = foodItemId,
        Quantity = quantity,
        Unit = unit,
        UnitPrice = unitPrice,
        QualityPercent = qualityPercent
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

    private sealed record Seed(Guid DonorId, Guid ReceiptId, Guid BreadId, Guid ApplesId);
}
