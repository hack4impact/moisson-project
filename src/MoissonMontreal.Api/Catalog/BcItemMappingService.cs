using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Catalog;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Creates, changes, deletes and finds the mappings between Business Central item codes and catalog items.
/// Every change is saved together with its history row, so a change is never saved without its author.
/// </summary>
/// <param name="db">Database context of the request.</param>
public class BcItemMappingService(MoissonDbContext db)
{
    /// <summary>
    /// Saves a mapping confirmed by a person, so the next imports reuse it without asking again.
    /// </summary>
    /// <param name="businessCentralCode">Item code from the Business Central export. Spaces around it are removed.</param>
    /// <param name="catalogItemId">Catalog item chosen to value this code.</param>
    /// <param name="userId">User who confirmed the mapping.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The saved mapping, with its catalog item.</returns>
    /// <exception cref="ArgumentException">The code is empty.</exception>
    /// <exception cref="CatalogItemNotFoundException">No catalog item has this identifier.</exception>
    /// <exception cref="CatalogItemInactiveException">The catalog item was removed from the price grid.</exception>
    /// <exception cref="BcItemMappingConflictException">The code already has a mapping.</exception>
    public async Task<BcItemMapping> CreateAsync(
        string businessCentralCode,
        Guid catalogItemId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(businessCentralCode);
        var catalogItem = await FindActiveCatalogItemAsync(catalogItemId, cancellationToken);

        if (await db.BcItemMappings.AnyAsync(m => m.BusinessCentralCode == code, cancellationToken))
        {
            throw new BcItemMappingConflictException(code);
        }

        var mapping = new BcItemMapping
        {
            BusinessCentralCode = code,
            CatalogItemId = catalogItem.Id,
            CatalogItem = catalogItem,
            ConfirmedByUserId = userId,
            ConfirmedAt = DateTimeOffset.UtcNow,
        };
        db.BcItemMappings.Add(mapping);
        AddChange(code, BcItemMappingAction.Created, null, catalogItem.Id, userId);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (CatalogSaveErrors.IsBcItemMappingCodeConflict(exception))
        {
            // Another request created the same code between the check above and this save.
            throw new BcItemMappingConflictException(code);
        }

        return mapping;
    }

    /// <summary>
    /// Points an existing mapping to another catalog item and records who confirmed it.
    /// Confirming the same catalog item again only renews the confirmation and adds no history row.
    /// </summary>
    /// <param name="businessCentralCode">Item code whose mapping changes. Spaces around it are removed.</param>
    /// <param name="catalogItemId">Catalog item now chosen to value this code.</param>
    /// <param name="userId">User who confirmed the change.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The changed mapping, with its catalog item.</returns>
    /// <exception cref="ArgumentException">The code is empty.</exception>
    /// <exception cref="BcItemMappingNotFoundException">The code has no mapping.</exception>
    /// <exception cref="CatalogItemNotFoundException">No catalog item has this identifier.</exception>
    /// <exception cref="CatalogItemInactiveException">The catalog item was removed from the price grid.</exception>
    public async Task<BcItemMapping> UpdateAsync(
        string businessCentralCode,
        Guid catalogItemId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(businessCentralCode);
        var mapping = await FindMappingAsync(code, cancellationToken);
        var catalogItem = await FindActiveCatalogItemAsync(catalogItemId, cancellationToken);

        var oldCatalogItemId = mapping.CatalogItemId;
        if (oldCatalogItemId != catalogItem.Id)
        {
            AddChange(code, BcItemMappingAction.Updated, oldCatalogItemId, catalogItem.Id, userId);
        }

        mapping.CatalogItemId = catalogItem.Id;
        mapping.CatalogItem = catalogItem;
        mapping.ConfirmedByUserId = userId;
        mapping.ConfirmedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return mapping;
    }

    /// <summary>
    /// Deletes a mapping, so the next import asks a person again for this code. The history keeps the deletion.
    /// </summary>
    /// <param name="businessCentralCode">Item code whose mapping is deleted. Spaces around it are removed.</param>
    /// <param name="userId">User who deleted the mapping.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <exception cref="ArgumentException">The code is empty.</exception>
    /// <exception cref="BcItemMappingNotFoundException">The code has no mapping.</exception>
    public async Task DeleteAsync(string businessCentralCode, int userId, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(businessCentralCode);
        var mapping = await FindMappingAsync(code, cancellationToken);

        db.BcItemMappings.Remove(mapping);
        AddChange(code, BcItemMappingAction.Deleted, mapping.CatalogItemId, null, userId);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Finds the remembered mappings of the codes of an import, with their catalog item, so the import can reuse them.
    /// A code that has no mapping is left out and must be matched by a person.
    /// </summary>
    /// <param name="businessCentralCodes">Codes read from the Business Central export. Empty codes are ignored.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The mappings found, by code. Check <see cref="BcItemMapping.NeedsReview"/> before using one.</returns>
    public async Task<IReadOnlyDictionary<string, BcItemMapping>> ResolveAsync(
        IEnumerable<string> businessCentralCodes,
        CancellationToken cancellationToken = default)
    {
        var codes = businessCentralCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct()
            .ToList();

        return await db.BcItemMappings
            .Include(m => m.CatalogItem)
            .Where(m => codes.Contains(m.BusinessCentralCode))
            .ToDictionaryAsync(m => m.BusinessCentralCode, cancellationToken);
    }

    /// <summary>
    /// Removes the spaces around a code and refuses an empty one.
    /// </summary>
    /// <param name="businessCentralCode">Code as received.</param>
    /// <returns>The code without spaces around it.</returns>
    /// <exception cref="ArgumentException">The code is empty.</exception>
    private static string NormalizeCode(string businessCentralCode)
    {
        if (string.IsNullOrWhiteSpace(businessCentralCode))
        {
            throw new ArgumentException("The Business Central code cannot be empty.", nameof(businessCentralCode));
        }

        return businessCentralCode.Trim();
    }

    /// <summary>
    /// Loads a catalog item that can receive a mapping.
    /// </summary>
    /// <param name="catalogItemId">Identifier of the catalog item.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The active catalog item.</returns>
    /// <exception cref="CatalogItemNotFoundException">No catalog item has this identifier.</exception>
    /// <exception cref="CatalogItemInactiveException">The catalog item was removed from the price grid.</exception>
    private async Task<CatalogItem> FindActiveCatalogItemAsync(Guid catalogItemId, CancellationToken cancellationToken)
    {
        var catalogItem = await db.CatalogItems.SingleOrDefaultAsync(c => c.Id == catalogItemId, cancellationToken)
            ?? throw new CatalogItemNotFoundException(catalogItemId);

        return catalogItem.IsActive ? catalogItem : throw new CatalogItemInactiveException(catalogItemId);
    }

    /// <summary>
    /// Loads the mapping of a code, with its catalog item.
    /// </summary>
    /// <param name="code">Code without spaces around it.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The mapping of the code.</returns>
    /// <exception cref="BcItemMappingNotFoundException">The code has no mapping.</exception>
    private async Task<BcItemMapping> FindMappingAsync(string code, CancellationToken cancellationToken) =>
        await db.BcItemMappings
            .Include(m => m.CatalogItem)
            .SingleOrDefaultAsync(m => m.BusinessCentralCode == code, cancellationToken)
        ?? throw new BcItemMappingNotFoundException(code);

    /// <summary>
    /// Adds a history row, saved with the next SaveChangesAsync together with the change it describes.
    /// </summary>
    /// <param name="code">Code whose mapping changed.</param>
    /// <param name="action">Kind of change.</param>
    /// <param name="oldCatalogItemId">Catalog item before the change, or null when the mapping is created.</param>
    /// <param name="newCatalogItemId">Catalog item after the change, or null when the mapping is deleted.</param>
    /// <param name="userId">User who made the change.</param>
    private void AddChange(string code, BcItemMappingAction action, Guid? oldCatalogItemId, Guid? newCatalogItemId, int userId) =>
        db.BcItemMappingChanges.Add(new BcItemMappingChange
        {
            BusinessCentralCode = code,
            Action = action,
            OldCatalogItemId = oldCatalogItemId,
            NewCatalogItemId = newCatalogItemId,
            ChangedByUserId = userId,
        });
}
