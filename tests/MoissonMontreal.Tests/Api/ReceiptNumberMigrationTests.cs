using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class ReceiptNumberMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migrate_StartsTheFoodDonationSeriesAtZero()
    {
        await using var context = CreateContext("receipt_numbers_empty");
        await context.Database.MigrateAsync();

        var counter = await context.ReceiptNumberCounters.SingleAsync();

        Assert.Equal(ReceiptNumber.Series, counter.Series);
        Assert.Equal(0, counter.LastNumber);
    }

    [Fact]
    public async Task Migrate_NumbersTheReceiptsIssuedBeforeIt_InTheOrderTheyWereIssued()
    {
        await using var context = CreateContext("receipt_numbers_existing");
        await context.GetService<IMigrator>().MigrateAsync("20261127052039_AddReceiptStatus");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (email, display_name, role_id) VALUES ('supply@moissonmontreal.org', 'Sam Supply', 1);
            INSERT INTO donors (name) VALUES ('Boulangerie Saint-Laurent');
            INSERT INTO receipts (donor_id, total_amount, status, created_at)
            SELECT id, 10, 'Issued', '2026-11-01T10:00:00Z' FROM donors;
            INSERT INTO receipts (donor_id, total_amount, status, created_at)
            SELECT id, 20, 'Cancelled', '2026-11-02T10:00:00Z' FROM donors;
            INSERT INTO receipts (donor_id, total_amount, status, created_at)
            SELECT id, 30, 'Approved', '2026-11-03T10:00:00Z' FROM donors;
            INSERT INTO receipts (donor_id, total_amount, status, created_at)
            SELECT id, 40, 'Issued', '2026-11-04T10:00:00Z' FROM donors;
            INSERT INTO receipt_status_changes (receipt_id, from_status, to_status, changed_by_user_id, changed_at)
            SELECT r.id, 'Approved', 'Issued', u.id, '2026-11-05T10:00:00Z' FROM receipts r, users u WHERE r.total_amount = 10;
            INSERT INTO receipt_status_changes (receipt_id, from_status, to_status, changed_by_user_id, changed_at)
            SELECT r.id, 'Approved', 'Issued', u.id, '2026-11-03T10:00:00Z' FROM receipts r, users u WHERE r.total_amount = 20;
            INSERT INTO receipt_status_changes (receipt_id, from_status, to_status, changed_by_user_id, changed_at, reason)
            SELECT r.id, 'Issued', 'Cancelled', u.id, '2026-11-06T10:00:00Z', 'Wrong quantity' FROM receipts r, users u WHERE r.total_amount = 20;
            """);

        await context.Database.MigrateAsync();

        var numbers = await context.Receipts.OrderBy(r => r.TotalAmount).Select(r => r.ReceiptNumber).ToListAsync();
        Assert.Equal(["DEN_000002", "DEN_000001", null, "DEN_000003"], numbers);
        Assert.Equal(3, (await context.ReceiptNumberCounters.SingleAsync()).LastNumber);
    }

    [Fact]
    public async Task Receipt_HasNoNumber_UntilItIsIssued()
    {
        var receiptId = await AddReceiptAsync(ReceiptStatus.Draft);

        await using var context = CreateContext();

        Assert.Null(await context.Receipts.Where(r => r.Id == receiptId).Select(r => r.ReceiptNumber).SingleAsync());
    }

    [Fact]
    public async Task Receipt_RejectsTheSameNumberTwice()
    {
        await AddReceiptAsync(ReceiptStatus.Issued, "DEN_100001");

        await using var context = CreateContext();
        context.Receipts.Add(NewReceipt(ReceiptStatus.Issued, "DEN_100001"));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ix_receipts_receipt_number");
    }

    [Fact]
    public async Task Receipt_RejectsAnIssuedReceiptWithoutNumber()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.Receipts.Add(NewReceipt(ReceiptStatus.Issued, null));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipts_issued_has_receipt_number");
    }

    [Theory]
    [InlineData(ReceiptStatus.Draft)]
    [InlineData(ReceiptStatus.PendingSupplyApproval)]
    [InlineData(ReceiptStatus.PendingFinanceApproval)]
    [InlineData(ReceiptStatus.Approved)]
    public async Task Receipt_RejectsANumber_BeforeItIsIssued(ReceiptStatus status)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.Receipts.Add(NewReceipt(status, "DEN_200001"));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipts_receipt_number_only_once_issued");
    }

    [Fact]
    public async Task Receipt_KeepsItsNumber_WhenItIsCancelled()
    {
        var receiptId = await AddReceiptAsync(ReceiptStatus.Issued, "DEN_300001");

        await using var context = CreateContext();
        await context.Receipts
            .Where(r => r.Id == receiptId)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, ReceiptStatus.Cancelled));

        Assert.Equal("DEN_300001", await context.Receipts.Where(r => r.Id == receiptId).Select(r => r.ReceiptNumber).SingleAsync());
    }

    [Theory]
    [InlineData("WEB_056836")]
    [InlineData("DEN_1")]
    [InlineData("DEN-000001")]
    [InlineData("den_000001")]
    public async Task Receipt_RejectsANumberInAnotherFormat(string number)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        context.Receipts.Add(NewReceipt(ReceiptStatus.Issued, number));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipts_receipt_number_format");
    }

    private async Task<Guid> AddReceiptAsync(ReceiptStatus status, string? number = null)
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var receipt = NewReceipt(status, number);
        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();
        return receipt.Id;
    }

    private static Receipt NewReceipt(ReceiptStatus status, string? number) => new()
    {
        Donor = new Donor { Name = "Boulangerie Saint-Laurent" },
        TotalAmount = 44.97m,
        Status = status,
        ReceiptNumber = number,
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
