using System.Globalization;

using ClosedXML.Excel;

using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Reads the lines of a purchase order (PO) exported from Business Central to Excel, and turns each article line into a catalog row.
/// Columns are found by their header name, so the import keeps working if Business Central adds or moves columns.
/// Every cell is read as text, and a formula is never calculated: only the value Excel saved with the file is read.
/// </summary>
public static class BusinessCentralExportReader
{
    /// <summary>
    /// Name of the sheet that holds the PO lines in a Business Central export.
    /// </summary>
    public const string SheetName = "Lignes";

    /// <summary>
    /// Category given to a new catalog item, since the export has no category. Moisson Montréal corrects it by hand.
    /// </summary>
    public const string DefaultCategory = "To classify";

    /// <summary>
    /// Quality class given to a new catalog item, since the export has no quality class. Moisson Montréal corrects it by hand.
    /// </summary>
    public const string DefaultQualityClass = "A";

    /// <summary>
    /// Value of the Type column on a line that is a food item. Other lines, such as comments, are not imported.
    /// </summary>
    private const string ArticleType = "Article";

    /// <summary>
    /// Header of the column that says what kind of line it is.
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
    /// Header of the column that holds the unit, such as CA (case) or KG.
    /// </summary>
    private const string UnitHeader = "Code d'unité de mesure";

    /// <summary>
    /// Reads every line of the export. Empty lines are skipped. Lines that are not articles are reported, not ignored.
    /// </summary>
    /// <param name="file">Content of the .xlsx file.</param>
    /// <returns>The article lines as catalog rows, and the lines that could not be read.</returns>
    /// <exception cref="BusinessCentralExportException">The file is not an Excel file, has no Lignes sheet, or misses a required column.</exception>
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

            if (type.Length == 0 && code.Length == 0 && name.Length == 0 && unit.Length == 0)
            {
                continue;
            }

            if (!string.Equals(type, ArticleType, StringComparison.OrdinalIgnoreCase))
            {
                var shownType = type.Length == 0 ? "empty" : type;
                errors.Add(new CatalogImportError(rowNumber, code, $"The line type is {shownType}, not {ArticleType}, so it is not a food item."));
                continue;
            }

            var item = new CatalogItemRequest(name, DefaultCategory, code, unit, DefaultQualityClass);
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
    private static string ReadText(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.ToString(CultureInfo.InvariantCulture).Trim();
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
/// <param name="Rows">Article lines, ready for <see cref="CatalogImporter"/>.</param>
/// <param name="Errors">Lines that are not articles, with their row number and the reason.</param>
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
