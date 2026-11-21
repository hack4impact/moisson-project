using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Tests.Fixtures;

/// <summary>
/// Simulates another request that adds a catalog item with the same Business Central code at the same time:
/// just before a save that adds catalog items, it saves that item through another connection, once.
/// </summary>
public sealed class SameCodeSavedMeanwhile(string connectionString, string businessCentralCode) : SaveChangesInterceptor
{
    private bool _done;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var addsCatalogItems = eventData.Context?.ChangeTracker.Entries<CatalogItem>().Any(entry => entry.State == EntityState.Added) ?? false;
        if (!_done && addsCatalogItems)
        {
            _done = true;
            await using var other = new MoissonDbContext(
                new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
            var item = TestCatalogItems.Create("Saved meanwhile");
            item.BusinessCentralCode = businessCentralCode;
            item.Unit = "CA";
            other.CatalogItems.Add(item);
            await other.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
