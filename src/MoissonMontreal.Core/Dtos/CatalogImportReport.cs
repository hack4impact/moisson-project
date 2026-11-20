namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Result of a catalog import: how many catalog items were created, updated or left unchanged, and the rows that were refused.
/// Each count is a number of distinct catalog items, not of rows, since one item can appear on several rows.
/// </summary>
/// <param name="Created">Number of catalog items added because their Business Central code was new.</param>
/// <param name="Updated">Number of existing catalog items whose values changed.</param>
/// <param name="Unchanged">Number of existing catalog items that already had the same values.</param>
/// <param name="Errors">Rows that were refused, in file order. The other rows are imported anyway.</param>
public record CatalogImportReport(
    int Created,
    int Updated,
    int Unchanged,
    IReadOnlyList<CatalogImportError> Errors);

/// <summary>
/// A row of the import file that was refused, with the reason.
/// </summary>
/// <param name="RowNumber">Number of the row in the file, as shown in Excel.</param>
/// <param name="BusinessCentralCode">Business Central code read on the row, or an empty string if the cell was empty.</param>
/// <param name="Message">Why the row was refused.</param>
public record CatalogImportError(
    int RowNumber,
    string BusinessCentralCode,
    string Message);
