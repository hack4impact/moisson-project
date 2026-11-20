using System.IO.Compression;

using ClosedXML.Excel;

using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Tests.Api;

public class BusinessCentralExportReaderTests
{
    // Same headers as a real PO export, including the curly apostrophe Business Central uses in the unit header.
    private static readonly string[] Headers =
        ["Type", "N°", "Withholding Tax Prod. Post. Group", "Description", "Code d'emplacement", "Quantité", "Code d’unité de mesure", "Qualité du test"];

    [Fact]
    public void Read_TurnsEachArticleLineIntoARow_WithTheDefaultCategoryAndQualityClass()
    {
        using var file = BuildExport(
            ["Article", "1000094", "", "Citron lime", "CDL", 48, "CA", "Bon"],
            ["Article", "1000769", "", "APS NON TRIÉ ", "CDL", 196, "KG", ""]);

        var export = BusinessCentralExportReader.Read(file);

        Assert.Empty(export.Errors);
        Assert.Equal(
            [
                new CatalogImportRow(2, new CatalogItemRequest("Citron lime", "To classify", "1000094", "CA", "A")),
                new CatalogImportRow(3, new CatalogItemRequest("APS NON TRIÉ", "To classify", "1000769", "KG", "A")),
            ],
            export.Rows);
    }

    [Fact]
    public void Read_KeepsANumericCode_AsText()
    {
        using var file = BuildExport(["Article", 1000112, "", "Poivron", "CDL", 100, "CA", "Moyen"]);

        var row = Assert.Single(BusinessCentralExportReader.Read(file).Rows);

        Assert.Equal("1000112", row.Item.BusinessCentralCode);
    }

    [Fact]
    public void Read_FindsColumnsByName_WhenTheyAreInAnotherOrder()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Lignes");
        sheet.Cell(1, 1).Value = "Code d'unité de mesure";
        sheet.Cell(1, 2).Value = "Description";
        sheet.Cell(1, 3).Value = "N°";
        sheet.Cell(1, 4).Value = "Type";
        sheet.Cell(2, 1).Value = "CA";
        sheet.Cell(2, 2).Value = "Tomate";
        sheet.Cell(2, 3).Value = "1000110";
        sheet.Cell(2, 4).Value = "Article";
        using var file = Save(workbook);

        var row = Assert.Single(BusinessCentralExportReader.Read(file).Rows);

        Assert.Equal(("1000110", "Tomate", "CA"), (row.Item.BusinessCentralCode, row.Item.Name, row.Item.Unit));
    }

    [Fact]
    public void Read_SkipsEmptyLines_AndKeepsTheExcelRowNumbers()
    {
        using var file = BuildExport(
            ["Article", "1000094", "", "Citron lime", "CDL", 48, "CA", "Bon"],
            ["", "", "", "", "", "", "", ""],
            ["Article", "1000106", "", "Brocoli", "CDL", 32, "CA", "Bon"]);

        var export = BusinessCentralExportReader.Read(file);

        Assert.Equal([2, 4], export.Rows.Select(row => row.RowNumber));
        Assert.Empty(export.Errors);
    }

    [Fact]
    public void Read_ReportsALineThatIsNotAnArticle_AndReadsTheOthers()
    {
        using var file = BuildExport(
            ["Article", "1000094", "", "Citron lime", "CDL", 48, "CA", "Bon"],
            ["Commentaire", "", "", "Livraison en retard", "", "", "", ""],
            ["Article", "1000106", "", "Brocoli", "CDL", 32, "CA", "Bon"]);

        var export = BusinessCentralExportReader.Read(file);

        Assert.Equal([2, 4], export.Rows.Select(row => row.RowNumber));
        Assert.Equal(
            new CatalogImportError(3, "", "The line type is Commentaire, not Article, so it is not a food item."),
            Assert.Single(export.Errors));
    }

    [Fact]
    public void Read_ReportsALineWithAnEmptyType()
    {
        using var file = BuildExport(["", "1000094", "", "Citron lime", "CDL", 48, "CA", "Bon"]);

        var export = BusinessCentralExportReader.Read(file);

        Assert.Empty(export.Rows);
        Assert.Equal(
            new CatalogImportError(2, "1000094", "The line type is empty, not Article, so it is not a food item."),
            Assert.Single(export.Errors));
    }

    [Fact]
    public void Read_ReadsTheSavedValue_OfAFormulaCell_WithoutCalculatingTheFormula()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddLignesSheet(workbook);
        sheet.Cell(2, 1).Value = "Article";
        sheet.Cell(2, 2).Value = "1000094";
        sheet.Cell(2, 4).FormulaA1 = "\"Citron\" & \" lime\"";
        sheet.Cell(2, 7).Value = "CA";
        using var saved = Save(workbook);

        // The saved value differs from what the formula gives, so reading "Citron lime" would mean the formula was calculated.
        using var file = WithSavedFormulaValue(saved, "D2", "Nom enregistré");

        var row = Assert.Single(BusinessCentralExportReader.Read(file).Rows);

        Assert.Equal("Nom enregistré", row.Item.Name);
    }

    [Fact]
    public void Read_ReportsALineWithAnExcelErrorInTheName_AndReadsTheOthers()
    {
        using var file = BuildExport(
            ["Article", "1000094", "", "Citron lime", "CDL", 48, "CA", "Bon"],
            ["Article", "1000112", "", XLError.CellReference, "CDL", 100, "CA", "Moyen"],
            ["Article", "1000106", "", "Brocoli", "CDL", 32, "CA", "Bon"]);

        var export = BusinessCentralExportReader.Read(file);

        Assert.Equal([2, 4], export.Rows.Select(row => row.RowNumber));
        Assert.Equal(
            new CatalogImportError(3, "1000112", "The Description cell contains the Excel error #REF!."),
            Assert.Single(export.Errors));
    }

    [Fact]
    public void Read_ReportsEveryExcelErrorOfALine_AndHidesACodeThatIsAnError()
    {
        using var file = BuildExport(["Article", XLError.NoValueAvailable, "", "Poivron", "CDL", 100, XLError.DivisionByZero, "Moyen"]);

        var export = BusinessCentralExportReader.Read(file);

        Assert.Empty(export.Rows);
        Assert.Equal(
            new CatalogImportError(2, "", "The N° cell contains the Excel error #N/A. The Code d'unité de mesure cell contains the Excel error #DIV/0!."),
            Assert.Single(export.Errors));
    }

    [Fact]
    public void Read_ReportsAFormulaCell_WhoseSavedResultIsAnExcelError()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddLignesSheet(workbook);
        sheet.Cell(2, 1).Value = "Article";
        sheet.Cell(2, 2).Value = "1000094";
        sheet.Cell(2, 4).FormulaA1 = "VLOOKUP(B2, Articles!A:B, 2, FALSE)";
        sheet.Cell(2, 7).Value = "CA";
        using var saved = Save(workbook);
        using var file = WithSavedFormulaValue(saved, "D2", "#N/A", valueType: "e");

        var export = BusinessCentralExportReader.Read(file);

        Assert.Empty(export.Rows);
        Assert.Equal(
            new CatalogImportError(2, "1000094", "The Description cell contains the Excel error #N/A."),
            Assert.Single(export.Errors));
    }

    [Fact]
    public void Read_ReturnsNoRowAndNoError_WhenTheSheetOnlyHasHeaders()
    {
        using var file = BuildExport();

        var export = BusinessCentralExportReader.Read(file);

        Assert.Empty(export.Rows);
        Assert.Empty(export.Errors);
    }

    [Fact]
    public void Read_Throws_WhenARequiredColumnIsMissing()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Lignes");
        sheet.Cell(1, 1).Value = "Type";
        sheet.Cell(1, 2).Value = "Description";
        using var file = Save(workbook);

        var exception = Assert.Throws<BusinessCentralExportException>(() => BusinessCentralExportReader.Read(file));

        Assert.Equal("The Lignes sheet is missing these columns: N°, Code d'unité de mesure.", exception.Message);
    }

    [Fact]
    public void Read_Throws_WhenThereIsNoLignesSheet()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Feuil1").Cell(1, 1).Value = "Type";
        using var file = Save(workbook);

        var exception = Assert.Throws<BusinessCentralExportException>(() => BusinessCentralExportReader.Read(file));

        Assert.Equal("The file does not have a sheet named Lignes.", exception.Message);
    }

    [Fact]
    public void Read_Throws_WhenTheFileIsNotAnExcelFile()
    {
        using var file = new MemoryStream("Type;N°;Description\nArticle;1000094;Citron lime"u8.ToArray());

        var exception = Assert.Throws<BusinessCentralExportException>(() => BusinessCentralExportReader.Read(file));

        Assert.Equal("The file is not a valid Excel (.xlsx) file.", exception.Message);
    }

    private static MemoryStream BuildExport(params XLCellValue[][] lines)
    {
        using var workbook = new XLWorkbook();
        var sheet = AddLignesSheet(workbook);
        for (var i = 0; i < lines.Length; i++)
        {
            for (var column = 0; column < lines[i].Length; column++)
            {
                sheet.Cell(i + 2, column + 1).Value = lines[i][column];
            }
        }

        return Save(workbook);
    }

    private static IXLWorksheet AddLignesSheet(XLWorkbook workbook)
    {
        var sheet = workbook.AddWorksheet("Lignes");
        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = Headers[column];
        }

        return sheet;
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // ClosedXML saves a formula without its result, so the result Excel would have saved is written into the sheet XML by hand.
    private static MemoryStream WithSavedFormulaValue(MemoryStream workbook, string cell, string savedValue, string valueType = "str")
    {
        var copy = new MemoryStream();
        workbook.CopyTo(copy);

        using (var zip = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }

            var cellStart = $"<x:c r=\"{cell}\" s=\"0\">";
            Assert.Contains(cellStart, xml);
            var cellIndex = xml.IndexOf(cellStart, StringComparison.Ordinal);
            var formulaEnd = xml.IndexOf("</x:f>", cellIndex, StringComparison.Ordinal) + "</x:f>".Length;
            xml = xml[..formulaEnd] + $"<x:v>{savedValue}</x:v>" + xml[formulaEnd..];
            xml = xml.Replace(cellStart, $"<x:c r=\"{cell}\" s=\"0\" t=\"{valueType}\">");

            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write(xml);
        }

        copy.Position = 0;
        return copy;
    }
}
