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
    public async Task ReceiptItem_StoresUnitPriceWithCents()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var saved = await context.ReceiptItems.SingleAsync(i => i.ReceiptId == seed.ReceiptId && i.FoodItemId == seed.ApplesId);

        Assert.Equal(2.49m, saved.UnitPrice);
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
    public async Task ReceiptItems_LineValuesAddUpToTotalAmount()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var lineValues = await context.ReceiptItems
            .Where(i => i.ReceiptId == seed.ReceiptId)
            .OrderBy(i => i.FoodItem.Name)
            .Select(i => i.FoodItem.Quantity * i.UnitPrice * i.QualityPercent / 100m)
            .ToListAsync();
        var total = await context.Receipts.Where(r => r.Id == seed.ReceiptId).Select(r => r.TotalAmount).SingleAsync();

        Assert.Equal([37.50m, 7.47m], lineValues);
        Assert.Equal(total, lineValues.Sum());
    }

    [Fact]
    public async Task ReceiptItem_AcceptsZeroPercent_ForQualityC()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var spoiled = new FoodItem
        {
            DonorId = seed.DonorId,
            Name = "Yogourt",
            Quantity = 2m,
            Unit = "kg",
            DonationDate = new DateOnly(2026, 11, 3),
            Quality = "C"
        };
        context.Receipts.Add(new Receipt
        {
            DonorId = seed.DonorId,
            TotalAmount = 0m,
            Items = [new ReceiptItem { FoodItem = spoiled, UnitPrice = 3.00m, QualityPercent = 0m }]
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
        context.ReceiptItems.Add(new ReceiptItem
        {
            ReceiptId = seed.ReceiptId,
            FoodItemId = Guid.NewGuid(),
            UnitPrice = 1m,
            QualityPercent = 100m
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_items_food_items_food_item_id");
    }

    [Fact]
    public async Task SaveReceiptItem_Throws_WhenFoodItemIsAlreadyOnTheReceipt()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.ReceiptItems.Add(new ReceiptItem
        {
            ReceiptId = seed.ReceiptId,
            FoodItemId = seed.BreadId,
            UnitPrice = 4.00m,
            QualityPercent = 75m
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "pk_receipt_items");
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

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_items_food_items_food_item_id");
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
    public async Task SaveChanges_Throws_WhenUnitPriceIsNotPositive(int unitPrice)
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.Receipts.Add(new Receipt
        {
            DonorId = seed.DonorId,
            TotalAmount = 0m,
            Items = [new ReceiptItem { FoodItemId = seed.BreadId, UnitPrice = unitPrice, QualityPercent = 100m }]
        });

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_items_unit_price_positive");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SaveChanges_Throws_WhenQualityPercentIsOutOfRange(int qualityPercent)
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.Receipts.Add(new Receipt
        {
            DonorId = seed.DonorId,
            TotalAmount = 0m,
            Items = [new ReceiptItem { FoodItemId = seed.BreadId, UnitPrice = 4.00m, QualityPercent = qualityPercent }]
        });

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
                new ReceiptItem { FoodItem = bread, UnitPrice = 4.00m, QualityPercent = 75m },
                new ReceiptItem { FoodItem = apples, UnitPrice = 2.49m, QualityPercent = 100m }
            ]
        };

        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        return new Seed(donor.Id, receipt.Id, bread.Id, apples.Id);
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

    private sealed record Seed(Guid DonorId, Guid ReceiptId, Guid BreadId, Guid ApplesId);
}