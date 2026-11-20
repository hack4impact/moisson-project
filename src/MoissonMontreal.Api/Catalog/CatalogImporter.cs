using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Saves the rows of a Business Central export to the food catalog.
/// A row whose Business Central code is new creates a catalog item, and a row whose code already exists updates that item,
/// so importing the same file twice does not create duplicates. Invalid rows are reported and do not stop the others.
/// An update only changes the name, which comes from Business Central. The unit, the category and the quality class are only set
/// when an item is created: prices are per unit, so changing the unit would silently change the meaning of every price of the item,
/// and the category and quality class are corrected by hand by Moisson Montréal. A row whose unit differs from the item's is reported.
/// </summary>
/// <param name="db">Database context of the request.</param>
public class CatalogImporter(MoissonDbContext db)
{
    /// <summary>
    /// Validates every row, then creates or updates the matching catalog items and saves them all at once.
    /// When several rows share a Business Central code, they all point to the same catalog item: the first valid row sets the unit
    /// of a new item, and the last valid row gives the name.
    /// </summary>
    /// <param name="rows">Rows read from the export file, in file order.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The number of items created, updated and unchanged, and the refused rows.</returns>
    /// <exception cref="CatalogImportConflictException">
    /// Another request added an item with one of the file's codes at the same time. Nothing is saved, and the import can be run again.
    /// </exception>
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

        foreach (var (rowNumber, fields) in validRows)
        {
            if (!itemsByCode.TryGetValue(fields.BusinessCentralCode, out var item))
            {
                item = new CatalogItem
                {
                    BusinessCentralCode = fields.BusinessCentralCode,
                    Name = fields.Name,
                    Unit = fields.Unit,
                    Category = fields.Category,
                    QualityClassCode = fields.QualityClass,
                };
                db.CatalogItems.Add(item);
                itemsByCode[fields.BusinessCentralCode] = item;
                createdCodes.Add(fields.BusinessCentralCode);
            }
            else if (!IsSameUnit(fields.Unit, item.Unit))
            {
                errors.Add(new CatalogImportError(
                    rowNumber,
                    fields.BusinessCentralCode,
                    $"The unit {fields.Unit} differs from the catalog unit {item.Unit}. Prices are per unit, so the item was not changed."));
            }
            else if (item.Name != fields.Name)
            {
                item.Name = fields.Name;

                // An item created earlier in this same file is still new, so it is counted as created only.
                if (existingCodes.Contains(fields.BusinessCentralCode))
                {
                    item.UpdatedAt = DateTimeOffset.UtcNow;
                    updatedCodes.Add(fields.BusinessCentralCode);
                }
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (CatalogSaveErrors.IsBusinessCentralCodeConflict(exception))
        {
            throw new CatalogImportConflictException(exception);
        }

        return new CatalogImportReport(
            Created: createdCodes.Count,
            Updated: updatedCodes.Count,
            Unchanged: existingCodes.Count - updatedCodes.Count,
            Errors: errors.OrderBy(error => error.RowNumber).ToList());
    }

    /// <summary>
    /// Tells whether two units are the same, ignoring spaces around them and upper or lower case, so kg and KG match.
    /// </summary>
    /// <param name="rowUnit">Unit read on the row.</param>
    /// <param name="itemUnit">Unit of the catalog item, which is the unit of its prices.</param>
    /// <returns>True if both are the same unit.</returns>
    private static bool IsSameUnit(string rowUnit, string itemUnit) =>
        string.Equals(rowUnit.Trim(), itemUnit.Trim(), StringComparison.OrdinalIgnoreCase);

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
