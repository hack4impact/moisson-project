using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Catalog;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class BcItemMappingServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private int _userId;

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        _userId = await AddUserAsync(context);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_SavesTheMapping_AndItsHistoryWithTheAuthor()
    {
        var item = await AddCatalogItemAsync();
        var code = NewCode();

        await using (var context = CreateContext())
        {
            var mapping = await new BcItemMappingService(context).CreateAsync(code, item.Id, _userId);
            Assert.Equal((code, item.Id, _userId), (mapping.BusinessCentralCode, mapping.CatalogItemId, mapping.ConfirmedByUserId));
            Assert.NotEqual(default, mapping.ConfirmedAt);
        }

        var change = Assert.Single(await HistoryAsync(code));
        Assert.Equal(BcItemMappingAction.Created, change.Action);
        Assert.Equal((null, item.Id, _userId), (change.OldCatalogItemId, change.NewCatalogItemId, change.ChangedByUserId));
        Assert.NotEqual(default, change.ChangedAt);
    }

    [Fact]
    public async Task Create_RemovesTheSpacesAroundTheCode()
    {
        var item = await AddCatalogItemAsync();
        var code = NewCode();

        await using var context = CreateContext();
        var mapping = await new BcItemMappingService(context).CreateAsync($"  {code} ", item.Id, _userId);

        Assert.Equal(code, mapping.BusinessCentralCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RefusesAnEmptyCode(string code)
    {
        var item = await AddCatalogItemAsync();

        await using var context = CreateContext();
        await Assert.ThrowsAsync<ArgumentException>(() => new BcItemMappingService(context).CreateAsync(code, item.Id, _userId));
    }

    [Fact]
    public async Task Create_RefusesASecondMappingForTheSameCode()
    {
        var first = await AddCatalogItemAsync();
        var second = await AddCatalogItemAsync();
        var code = NewCode();
        await CreateAsync(code, first.Id);

        await using (var context = CreateContext())
        {
            var exception = await Assert.ThrowsAsync<BcItemMappingConflictException>(
                () => new BcItemMappingService(context).CreateAsync(code, second.Id, _userId));
            Assert.Equal(code, exception.BusinessCentralCode);
        }

        Assert.Equal(first.Id, (await FindMappingAsync(code))!.CatalogItemId);
        Assert.Single(await HistoryAsync(code));
    }

    [Fact]
    public async Task Create_RefusesTheCode_WhenAnotherRequestMappedItMeanwhile()
    {
        var item = await AddCatalogItemAsync();
        var other = await AddCatalogItemAsync();
        var code = NewCode();

        await using (var context = CreateContext(new SameMappingSavedMeanwhile(postgres.ConnectionString, code, other.Id, _userId)))
        {
            await Assert.ThrowsAsync<BcItemMappingConflictException>(
                () => new BcItemMappingService(context).CreateAsync(code, item.Id, _userId));
        }

        // Only the mapping of the other request exists, and the refused one left no history row.
        Assert.Equal(other.Id, (await FindMappingAsync(code))!.CatalogItemId);
        Assert.Equal(other.Id, Assert.Single(await HistoryAsync(code)).NewCatalogItemId);
    }

    [Fact]
    public async Task Create_RefusesACatalogItemThatDoesNotExist()
    {
        var missing = Guid.NewGuid();
        var code = NewCode();

        await using (var context = CreateContext())
        {
            var exception = await Assert.ThrowsAsync<CatalogItemNotFoundException>(
                () => new BcItemMappingService(context).CreateAsync(code, missing, _userId));
            Assert.Equal(missing, exception.CatalogItemId);
        }

        Assert.Null(await FindMappingAsync(code));
    }

    [Fact]
    public async Task Create_RefusesAnInactiveCatalogItem()
    {
        var item = await AddCatalogItemAsync(isActive: false);
        var code = NewCode();

        await using (var context = CreateContext())
        {
            await Assert.ThrowsAsync<CatalogItemInactiveException>(
                () => new BcItemMappingService(context).CreateAsync(code, item.Id, _userId));
        }

        Assert.Null(await FindMappingAsync(code));
    }

    [Fact]
    public async Task Update_PointsToTheNewItem_AndLogsTheOldAndNewItems()
    {
        var before = await AddCatalogItemAsync();
        var after = await AddCatalogItemAsync();
        var code = NewCode();
        await CreateAsync(code, before.Id);

        await using (var context = CreateContext())
        {
            var mapping = await new BcItemMappingService(context).UpdateAsync(code, after.Id, _userId);
            Assert.Equal(after.Id, mapping.CatalogItemId);
        }

        Assert.Equal(after.Id, (await FindMappingAsync(code))!.CatalogItemId);
        var update = (await HistoryAsync(code)).Single(c => c.Action == BcItemMappingAction.Updated);
        Assert.Equal((before.Id, after.Id, _userId), (update.OldCatalogItemId, update.NewCatalogItemId, update.ChangedByUserId));
    }

    [Fact]
    public async Task Update_WithTheSameItem_RenewsTheConfirmation_WithoutAHistoryRow()
    {
        var item = await AddCatalogItemAsync();
        var code = NewCode();
        var created = await CreateAsync(code, item.Id);

        await using (var context = CreateContext())
        {
            await new BcItemMappingService(context).UpdateAsync(code, item.Id, _userId);
        }

        Assert.True((await FindMappingAsync(code))!.ConfirmedAt > created.ConfirmedAt);
        Assert.Single(await HistoryAsync(code));
    }

    [Fact]
    public async Task Update_RefusesACodeWithoutMapping()
    {
        var item = await AddCatalogItemAsync();
        var code = NewCode();

        await using var context = CreateContext();
        var exception = await Assert.ThrowsAsync<BcItemMappingNotFoundException>(
            () => new BcItemMappingService(context).UpdateAsync(code, item.Id, _userId));
        Assert.Equal(code, exception.BusinessCentralCode);
    }

    [Fact]
    public async Task Update_RefusesAnInactiveCatalogItem_AndKeepsTheMapping()
    {
        var item = await AddCatalogItemAsync();
        var inactive = await AddCatalogItemAsync(isActive: false);
        var code = NewCode();
        await CreateAsync(code, item.Id);

        await using (var context = CreateContext())
        {
            await Assert.ThrowsAsync<CatalogItemInactiveException>(
                () => new BcItemMappingService(context).UpdateAsync(code, inactive.Id, _userId));
        }

        Assert.Equal(item.Id, (await FindMappingAsync(code))!.CatalogItemId);
        Assert.Single(await HistoryAsync(code));
    }

    [Fact]
    public async Task Delete_RemovesTheMapping_AndKeepsTheHistory()
    {
        var item = await AddCatalogItemAsync();
        var code = NewCode();
        await CreateAsync(code, item.Id);

        await using (var context = CreateContext())
        {
            await new BcItemMappingService(context).DeleteAsync(code, _userId);
        }

        Assert.Null(await FindMappingAsync(code));
        var history = await HistoryAsync(code);
        Assert.Equal([BcItemMappingAction.Created, BcItemMappingAction.Deleted], history.Select(c => c.Action));
        var deletion = history[1];
        Assert.Equal((item.Id, null, _userId), (deletion.OldCatalogItemId, deletion.NewCatalogItemId, deletion.ChangedByUserId));
    }

    [Fact]
    public async Task Delete_RefusesACodeWithoutMapping()
    {
        await using var context = CreateContext();
        await Assert.ThrowsAsync<BcItemMappingNotFoundException>(
            () => new BcItemMappingService(context).DeleteAsync(NewCode(), _userId));
    }

    [Fact]
    public async Task Delete_ThenCreate_LetsAPersonMatchTheCodeAgain()
    {
        var first = await AddCatalogItemAsync();
        var second = await AddCatalogItemAsync();
        var code = NewCode();
        await CreateAsync(code, first.Id);

        await using (var context = CreateContext())
        {
            await new BcItemMappingService(context).DeleteAsync(code, _userId);
        }

        await CreateAsync(code, second.Id);

        Assert.Equal(second.Id, (await FindMappingAsync(code))!.CatalogItemId);
        Assert.Equal(3, (await HistoryAsync(code)).Count);
    }

    [Fact]
    public async Task Resolve_ReturnsTheRememberedMappings_AndLeavesOutUnknownCodes()
    {
        var pepper = await AddCatalogItemAsync();
        var tomato = await AddCatalogItemAsync();
        var pepperCode = NewCode();
        var tomatoCode = NewCode();
        var unknownCode = NewCode();
        await CreateAsync(pepperCode, pepper.Id);
        await CreateAsync(tomatoCode, tomato.Id);

        await using var context = CreateContext();
        var resolved = await new BcItemMappingService(context).ResolveAsync(
            [pepperCode, $" {pepperCode} ", tomatoCode, unknownCode, "", "  "]);

        Assert.Equal(2, resolved.Count);
        Assert.Equal(pepper.Id, resolved[pepperCode].CatalogItem.Id);
        Assert.Equal(tomato.Id, resolved[tomatoCode].CatalogItem.Id);
        Assert.False(resolved.ContainsKey(unknownCode));
        Assert.All(resolved.Values, mapping => Assert.False(mapping.NeedsReview));
    }

    [Fact]
    public async Task Resolve_FlagsTheMappingToReview_WhenItsCatalogItemIsDeactivated()
    {
        var item = await AddCatalogItemAsync();
        var code = NewCode();
        await CreateAsync(code, item.Id);

        await using (var context = CreateContext())
        {
            await context.CatalogItems
                .Where(c => c.Id == item.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.IsActive, false));
        }

        await using var reader = CreateContext();
        var resolved = await new BcItemMappingService(reader).ResolveAsync([code]);

        Assert.True(resolved[code].NeedsReview);
    }

    private static string NewCode() => "BC-" + Guid.NewGuid().ToString("N")[..12];

    private static async Task<int> AddUserAsync(MoissonDbContext context)
    {
        var roleId = await context.Roles.Where(r => r.Code == RoleCodes.Supply).Select(r => r.Id).SingleAsync();
        var user = new User
        {
            Email = $"mapping.{Guid.NewGuid():N}@moissonmontreal.org",
            DisplayName = "Mapping Tester",
            RoleId = roleId,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task<CatalogItem> AddCatalogItemAsync(bool isActive = true)
    {
        await using var context = CreateContext();
        var item = TestCatalogItems.Create("Poivron");
        item.IsActive = isActive;
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    private async Task<BcItemMapping> CreateAsync(string code, Guid catalogItemId)
    {
        await using var context = CreateContext();
        return await new BcItemMappingService(context).CreateAsync(code, catalogItemId, _userId);
    }

    private async Task<BcItemMapping?> FindMappingAsync(string code)
    {
        await using var context = CreateContext();
        return await context.BcItemMappings.SingleOrDefaultAsync(m => m.BusinessCentralCode == code);
    }

    private async Task<List<BcItemMappingChange>> HistoryAsync(string code)
    {
        await using var context = CreateContext();
        return await context.BcItemMappingChanges
            .Where(c => c.BusinessCentralCode == code)
            .OrderBy(c => c.ChangedAt)
            .ToListAsync();
    }

    private MoissonDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).AddInterceptors(interceptors).Options);

    // Saves a mapping with the same code from another connection just before the tested save, as a second request would.
    private sealed class SameMappingSavedMeanwhile(string connectionString, string code, Guid catalogItemId, int userId)
        : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = new MoissonDbContext(
                    new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
                await new BcItemMappingService(other).CreateAsync(code, catalogItemId, userId, cancellationToken);
            }

            return result;
        }
    }
}
