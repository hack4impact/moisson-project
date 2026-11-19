using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Saves the rows of a Business Central export to the food catalog.
/// A row whose Business Central code is new creates a catalog item, and a row whose code already exists updates that item,
/// so importing the same file twice does not create duplicates. Invalid rows are reported and do not stop the others.
/// </summary>
/// <param name="db">Database context of the request.</param>
public class CatalogImporter(MoissonDbContext db)
{
    /// <summary>
    /// Validates every row, then creates or updates the matching catalog items and saves them all at once.
    /// When several rows share a Business Central code, they all point to the same catalog item, and the last valid row gives its values.
    /// </summary>
    /// <param name="rows">Rows read from the export file, in file order.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The number of items created, updated and unchanged, and the refused rows.</returns>
    public async Task<CatalogImportReport> ImportAsync(IReadOnlyList<CatalogImportRow> rows, CancellationToken cancellationToken = default)
    {
        var errors = new List<CatalogImportError>();
        var validRows = new List<(int RowNumber, CatalogItemFields Fields)>();

        var qualityClasses = (await db.QualityClasses
            .Select(quality => quality.Code)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var row in rows)
        {
            if (!CatalogItemValidator.TryNormalize(row.Item, out var fields, out var fieldErrors))
            {
                errors.Add(new CatalogImportError(row.RowNumber, fields.BusinessCentralCode, JoinMessages(fieldErrors)));
                continue;
            }

            if (!qualityClasses.Contains(fields.QualityClass))
            {
                errors.Add(new CatalogImportError(
                    row.RowNumber,
                    fields.BusinessCentralCode,
                    $"Quality class {fields.QualityClass} does not exist."));
                continue;
            }

            validRows.Add((row.RowNumber, fields));
        }

        var codes = validRows.Select(row => row.Fields.BusinessCentralCode).Distinct().ToList();
        var itemsByCode = await db.CatalogItems
            .Where(item => codes.Contains(item.BusinessCentralCode))
            .ToDictionaryAsync(item => item.BusinessCentralCode, cancellationToken);

        var existingCodes = itemsByCode.Keys.ToHashSet();
        var createdCodes = new HashSet<string>();
        var updatedCodes = new HashSet<string>();

        foreach (var (_, fields) in validRows)
        {
            if (!itemsByCode.TryGetValue(fields.BusinessCentralCode, out var item))
            {
                item = new CatalogItem { BusinessCentralCode = fields.BusinessCentralCode };
                CopyFields(fields, item);
                db.CatalogItems.Add(item);
                itemsByCode[fields.BusinessCentralCode] = item;
                createdCodes.Add(fields.BusinessCentralCode);
            }
            else if (HasChanges(fields, item))
            {
                CopyFields(fields, item);

                // An item created earlier in this same file is still new, so it is counted as created only.
                if (existingCodes.Contains(fields.BusinessCentralCode))
                {
                    item.UpdatedAt = DateTimeOffset.UtcNow;
                    updatedCodes.Add(fields.BusinessCentralCode);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CatalogImportReport(
            Created: createdCodes.Count,
            Updated: updatedCodes.Count,
            Unchanged: existingCodes.Count - updatedCodes.Count,
            Errors: errors);
    }

    /// <summary>
    /// Tells whether the values of a row differ from those already stored on a catalog item.
    /// </summary>
    /// <param name="fields">Validated values of the row.</param>
    /// <param name="item">Catalog item with the same Business Central code.</param>
    /// <returns>True if at least one value is different.</returns>
    private static bool HasChanges(CatalogItemFields fields, CatalogItem item) =>
        item.Name != fields.Name
        || item.Category != fields.Category
        || item.Unit != fields.Unit
        || item.QualityClassCode != fields.QualityClass;

    /// <summary>
    /// Copies the values of a row onto a catalog item. The Business Central code is not copied, since it is what matched the two.
    /// </summary>
    /// <param name="fields">Validated values of the row.</param>
    /// <param name="item">Catalog item to fill.</param>
    private static void CopyFields(CatalogItemFields fields, CatalogItem item)
    {
        item.Name = fields.Name;
        item.Category = fields.Category;
        item.Unit = fields.Unit;
        item.QualityClassCode = fields.QualityClass;
    }

    /// <summary>
    /// Joins the validation messages of a row into one sentence for the report.
    /// </summary>
    /// <param name="fieldErrors">Messages found by <see cref="CatalogItemValidator"/>, by field.</param>
    /// <returns>Every message, separated by a space.</returns>
    private static string JoinMessages(Dictionary<string, string[]> fieldErrors) =>
        string.Join(" ", fieldErrors.Values.SelectMany(messages => messages));
}

/// <summary>
/// One row of a Business Central export, already read from the file.
/// </summary>
/// <param name="RowNumber">Number of the row in the file, as shown in Excel, so an error can point to it.</param>
/// <param name="Item">Values read on the row, in the same shape the catalog API receives.</param>
public record CatalogImportRow(int RowNumber, CatalogItemRequest Item);
