using System.Globalization;

using ClosedXML.Excel;

using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Reads the item list (Articles) exported from Business Central to Excel, and turns each inventory item into a catalog row.
/// Only the code, the name and the base unit are read: the export has no category nor quality class, and its costs are not fair market values.
/// Columns are found by their header name, so the import keeps working if Business Central adds or moves columns.
/// Every cell is read as text, and a formula is never calculated: only the value Excel saved with the file is read.
/// </summary>
public static class BusinessCentralExportReader
{
    /// <summary>
    /// Name of the sheet that holds the items in a Business Central item list export.
    /// </summary>
    public const string SheetName = "Articles";

    /// <summary>
    /// Category given to a new catalog item, since the export has no category. Moisson Montréal corrects it by hand.
    /// </summary>
    public const string DefaultCategory = "To classify";

    /// <summary>
    /// Value of the Type column on an item that is food kept in stock. Other types, such as services, are not imported.
    /// </summary>
    private const string InventoryType = "Inventaire";

    /// <summary>
    /// Header of the column that says what kind of item it is.
    /// </summary>
    private const string TypeHeader = "Type";

    /// <summary>
    /// Header of the column that holds the Business Central code of the item.
    /// </summary>
    private const string CodeHeader = "N°";

    /// <summary>
    /// Header of the column that holds the name of the item.
    /// </summary>
    private const string NameHeader = "Description";

    /// <summary>
    /// Header of the column that holds the base unit of the item, such as KG, UN (unit) or CA (case). Prices are per this unit.
    /// </summary>
    private const string UnitHeader = "Unité de mesure de base";

    /// <summary>
    /// Reads every row of the export. Empty rows are skipped. Items whose type is not Inventaire, and rows with an Excel error
    /// such as #REF! in a required cell, are reported, not ignored.
    /// </summary>
    /// <param name="file">Content of the .xlsx file.</param>
    /// <returns>The inventory items as catalog rows, and the rows that could not be read.</returns>
    /// <exception cref="BusinessCentralExportException">The file is not an Excel file, has no Articles sheet, or misses a required column.</exception>
    public static BusinessCentralExport Read(Stream file)
    {
        using var workbook = OpenWorkbook(file);

        if (!workbook.TryGetWorksheet(SheetName, out var sheet))
        {
            throw new BusinessCentralExportException($"The file does not have a sheet named {SheetName}.");
        }

        var columns = FindColumns(sheet.Row(1));
        var rows = new List<CatalogImportRow>();
        var errors = new List<CatalogImportError>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var line = sheet.Row(rowNumber);
            var type = ReadText(line.Cell(columns.Type));
            var code = ReadText(line.Cell(columns.Code));
            var name = ReadText(line.Cell(columns.Name));
            var unit = ReadText(line.Cell(columns.Unit));

            var excelErrors = FindExcelErrors(line, columns);
            if (excelErrors.Count > 0)
            {
                var shownCode = SavedValue(line.Cell(columns.Code)).IsError ? string.Empty : code;
                errors.Add(new CatalogImportError(rowNumber, shownCode, string.Join(" ", excelErrors)));
                continue;
            }

            if (type.Length == 0 && code.Length == 0 && name.Length == 0 && unit.Length == 0)
            {
                continue;
            }

            if (!string.Equals(type, InventoryType, StringComparison.OrdinalIgnoreCase))
            {
                var shownType = type.Length == 0 ? "empty" : type;
                errors.Add(new CatalogImportError(rowNumber, code, $"The item type is {shownType}, not {InventoryType}, so it is not a food item."));
                continue;
            }

            // The export has no quality class. It stays empty until someone picks it, so a forgotten default never values a receipt.
            var item = new CatalogItemRequest(name, DefaultCategory, code, unit, QualityClass: null);
            rows.Add(new CatalogImportRow(rowNumber, item));
        }

        return new BusinessCentralExport(rows, errors);
    }

    /// <summary>
    /// Opens the stream as an Excel workbook.
    /// </summary>
    /// <param name="file">Content of the file.</param>
    /// <returns>The opened workbook. The caller disposes it.</returns>
    /// <exception cref="BusinessCentralExportException">The file is not a valid .xlsx file.</exception>
    private static XLWorkbook OpenWorkbook(Stream file)
    {
        try
        {
            return new XLWorkbook(file);
        }
        // ClosedXML throws several exception types for a damaged or non-Excel file, so they are all turned into one clear message.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new BusinessCentralExportException("The file is not a valid Excel (.xlsx) file.", exception);
        }
    }

    /// <summary>
    /// Finds the position of each required column in the header row.
    /// </summary>
    /// <param name="header">First row of the sheet.</param>
    /// <returns>The column number of each required column.</returns>
    /// <exception cref="BusinessCentralExportException">One or more required columns are missing.</exception>
    private static ExportColumns FindColumns(IXLRow header)
    {
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
        {
            positions.TryAdd(NormalizeHeader(ReadText(cell)), cell.Address.ColumnNumber);
        }

        string[] required = [TypeHeader, CodeHeader, NameHeader, UnitHeader];
        var missing = required.Where(name => !positions.ContainsKey(NormalizeHeader(name))).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessCentralExportException($"The {SheetName} sheet is missing these columns: {string.Join(", ", missing)}.");
        }

        return new ExportColumns(
            Type: positions[NormalizeHeader(TypeHeader)],
            Code: positions[NormalizeHeader(CodeHeader)],
            Name: positions[NormalizeHeader(NameHeader)],
            Unit: positions[NormalizeHeader(UnitHeader)]);
    }

    /// <summary>
    /// Makes two spellings of the same header equal. Business Central writes some headers with a curly apostrophe (’) and others with a straight one (').
    /// </summary>
    /// <param name="header">Header as written in the file.</param>
    /// <returns>The header without surrounding spaces and with straight apostrophes.</returns>
    private static string NormalizeHeader(string header) => header.Replace('’', '\'').Trim();

    /// <summary>
    /// Reads a cell as text. For a formula cell, the value saved in the file is read and the formula is never calculated.
    /// </summary>
    /// <param name="cell">Cell to read.</param>
    /// <returns>The text of the cell without surrounding spaces, or an empty string for an empty cell.</returns>
    private static string ReadText(IXLCell cell) =>
        SavedValue(cell).ToString(CultureInfo.InvariantCulture).Trim();

    /// <summary>
    /// Returns the value saved in a cell. For a formula cell, it is the result Excel saved with the file, and the formula is never calculated.
    /// </summary>
    /// <param name="cell">Cell to read.</param>
    /// <returns>The saved value, which can be text, a number, a date, empty or an Excel error.</returns>
    private static XLCellValue SavedValue(IXLCell cell) => cell.HasFormula ? cell.CachedValue : cell.Value;

    /// <summary>
    /// Lists the required cells of a line that contain an Excel error, such as #REF! or #N/A, so the line is reported instead of
    /// importing an item named after the error.
    /// </summary>
    /// <param name="line">Row of the sheet.</param>
    /// <param name="columns">Column number of each required column.</param>
    /// <returns>One message per cell with an error, in column order. Empty when no cell has an error.</returns>
    private static List<string> FindExcelErrors(IXLRow line, ExportColumns columns)
    {
        (string Header, int Column)[] requiredCells =
        [
            (TypeHeader, columns.Type),
            (CodeHeader, columns.Code),
            (NameHeader, columns.Name),
            (UnitHeader, columns.Unit),
        ];

        return requiredCells
            .Select(cell => (cell.Header, Value: SavedValue(line.Cell(cell.Column))))
            .Where(cell => cell.Value.IsError)
            .Select(cell => $"The {cell.Header} cell contains the Excel error {cell.Value.ToString(CultureInfo.InvariantCulture)}.")
            .ToList();
    }

    /// <summary>
    /// Column number of each required column in the sheet.
    /// </summary>
    /// <param name="Type">Column of the line type.</param>
    /// <param name="Code">Column of the Business Central code.</param>
    /// <param name="Name">Column of the item name.</param>
    /// <param name="Unit">Column of the unit.</param>
    private readonly record struct ExportColumns(int Type, int Code, int Name, int Unit);
}

/// <summary>
/// Content read from a Business Central export: the lines to import and the lines that could not be read.
/// </summary>
/// <param name="Rows">Inventory items, ready for <see cref="CatalogImporter"/>.</param>
/// <param name="Errors">Rows that could not be read or are not inventory items, with their row number and the reason.</param>
public record BusinessCentralExport(IReadOnlyList<CatalogImportRow> Rows, IReadOnlyList<CatalogImportError> Errors);

/// <summary>
/// Thrown when a whole Business Central export cannot be read, for example when it is not an Excel file or misses a required column.
/// A problem on a single line is reported in <see cref="BusinessCentralExport.Errors"/> instead.
/// </summary>
public class BusinessCentralExportException : Exception
{
    /// <summary>
    /// Creates the exception with a message that explains what is wrong with the file.
    /// </summary>
    /// <param name="message">Explanation shown to the person who imported the file.</param>
    public BusinessCentralExportException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the error that caused it.
    /// </summary>
    /// <param name="message">Explanation shown to the person who imported the file.</param>
    /// <param name="innerException">Error raised while opening the file.</param>
    public BusinessCentralExportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
