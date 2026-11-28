using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class ReceiptStatusMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Receipt_StartsAsDraft()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var receipt = await context.Receipts.SingleAsync(r => r.Id == seed.ReceiptId);

        Assert.Equal(ReceiptStatus.Draft, receipt.Status);
    }

    [Fact]
    public async Task Receipt_StoresTheStatusAsItsName()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        await context.Receipts
            .Where(r => r.Id == seed.ReceiptId)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, ReceiptStatus.PendingFinanceApproval));
        var stored = await context.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM receipts WHERE id = {seed.ReceiptId}")
            .SingleAsync();

        Assert.Equal("PendingFinanceApproval", stored);
    }

    [Fact]
    public async Task Receipt_RejectsAnUnknownStatus()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"UPDATE receipts SET status = 'Lost' WHERE id = {seed.ReceiptId}"));

        Assert.Equal("ck_receipts_status_valid", error.ConstraintName);
    }

    [Fact]
    public async Task Migrate_SetsExistingReceiptsToDraft()
    {
        await using var context = CreateContext("receipt_status_existing");
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261124214039_AddBcItemMappings");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO donors (name) VALUES ('Boulangerie Saint-Laurent');
            INSERT INTO receipts (donor_id, total_amount) SELECT id, 10 FROM donors;
            """);

        await migrator.MigrateAsync();

        var receipt = await context.Receipts.SingleAsync();
        Assert.Equal(ReceiptStatus.Draft, receipt.Status);
    }

    [Fact]
    public async Task StatusChange_RecordsWhoMadeItAndWhen()
    {
        var seed = await SeedAsync();
        var before = DateTimeOffset.UtcNow;

        await using (var saveContext = CreateContext())
        {
            saveContext.ReceiptStatusChanges.Add(Change(seed, ReceiptStatus.Draft, ReceiptStatus.PendingSupplyApproval));
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var change = await context.ReceiptStatusChanges.Include(c => c.ChangedBy).SingleAsync(c => c.ReceiptId == seed.ReceiptId);

        Assert.Equal(seed.UserEmail, change.ChangedBy.Email);
        Assert.InRange(change.ChangedAt, before.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Null(change.Reason);
    }

    [Theory]
    [InlineData(ReceiptStatus.PendingSupplyApproval, ReceiptStatus.Draft)]
    [InlineData(ReceiptStatus.PendingFinanceApproval, ReceiptStatus.Draft)]
    [InlineData(ReceiptStatus.Issued, ReceiptStatus.Cancelled)]
    public async Task StatusChange_RequiresAReason_WhenTheReceiptIsRefusedOrCancelled(ReceiptStatus from, ReceiptStatus to)
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.ReceiptStatusChanges.Add(Change(seed, from, to));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_status_changes_reason_required");
    }

    [Fact]
    public async Task StatusChange_KeepsTheReason_WhenTheReceiptIsCancelled()
    {
        var seed = await SeedAsync();

        await using (var saveContext = CreateContext())
        {
            saveContext.ReceiptStatusChanges.Add(Change(seed, ReceiptStatus.Issued, ReceiptStatus.Cancelled, "Wrong donor"));
            await saveContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var change = await context.ReceiptStatusChanges.SingleAsync(c => c.ReceiptId == seed.ReceiptId);

        Assert.Equal("Wrong donor", change.Reason);
    }

    [Fact]
    public async Task StatusChange_RejectsAnEmptyReason()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.ReceiptStatusChanges.Add(Change(seed, ReceiptStatus.Issued, ReceiptStatus.Cancelled, ""));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_status_changes_reason_not_empty");
    }

    [Fact]
    public async Task StatusChange_RejectsTheSameStatusBeforeAndAfter()
    {
        var seed = await SeedAsync();

        await using var context = CreateContext();
        context.ReceiptStatusChanges.Add(Change(seed, ReceiptStatus.Approved, ReceiptStatus.Approved));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "ck_receipt_status_changes_status_differs");
    }

    [Fact]
    public async Task Receipt_CannotBeDeleted_OnceItsStatusChanged()
    {
        var seed = await SeedAsync();
        await AddChangeAsync(seed);

        await using var context = CreateContext();
        context.Receipts.Remove(await context.Receipts.SingleAsync(r => r.Id == seed.ReceiptId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_status_changes_receipts_receipt_id");
    }

    [Fact]
    public async Task User_CannotBeDeleted_OnceTheyChangedAStatus()
    {
        var seed = await SeedAsync();
        await AddChangeAsync(seed);

        await using var context = CreateContext();
        context.Users.Remove(await context.Users.SingleAsync(u => u.Id == seed.UserId));

        await PostgresConstraintAssertions.AssertViolatesConstraintAsync(context, "fk_receipt_status_changes_users_changed_by_user_id");
    }

    private async Task AddChangeAsync(Seed seed)
    {
        await using var context = CreateContext();
        context.ReceiptStatusChanges.Add(Change(seed, ReceiptStatus.Draft, ReceiptStatus.PendingSupplyApproval));
        await context.SaveChangesAsync();
    }

    private static ReceiptStatusChange Change(Seed seed, ReceiptStatus from, ReceiptStatus to, string? reason = null) => new()
    {
        ReceiptId = seed.ReceiptId,
        FromStatus = from,
        ToStatus = to,
        ChangedByUserId = seed.UserId,
        Reason = reason,
    };

    private async Task<Seed> SeedAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var supplyRoleId = await context.Roles.Where(r => r.Code == RoleCodes.Supply).Select(r => r.Id).SingleAsync();
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@moissonmontreal.org",
            DisplayName = "Supply Employee",
            RoleId = supplyRoleId,
        };
        var receipt = new Receipt { Donor = new Donor { Name = "Boulangerie Saint-Laurent" }, TotalAmount = 44.97m };
        context.Users.Add(user);
        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        return new Seed(receipt.Id, user.Id, user.Email);
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

    private sealed record Seed(Guid ReceiptId, int UserId, string UserEmail);
}
