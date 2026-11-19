using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Controllers;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class CatalogImportEndpointTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private const string Route = "/api/catalog-items/import";

    // Same headers as a real PO export, including the curly apostrophe Business Central uses in the unit header.
    private static readonly string[] Headers = ["Type", "N°", "Description", "Code d’unité de mesure"];

    [Fact]
    public async Task Import_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Route, Form(BuildExport(["Article", "IMP-401", "Poivron", "CA"])));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    public async Task Import_ReturnsForbidden_WhenRoleCannotManageThePriceGrid(string role)
    {
        using var client = await CreateClientAsync(role);

        var response = await client.PostAsync(Route, Form(BuildExport(["Article", $"IMP-403-{role}", "Poivron", "CA"])));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountAsync($"IMP-403-{role}"));
    }

    [Fact]
    public async Task Import_CreatesTheItems_AndReturnsTheReport()
    {
        using var client = await CreateClientAsync();
        using var export = BuildExport(
            ["Article", "IMP-LIME", "Citron lime", "CA"],
            ["Article", "IMP-PEPPER", "Poivron", "CA"],
            ["Article", "IMP-PEPPER", "Poivron", "CA"]);

        var response = await client.PostAsync(Route, Form(export));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<CatalogImportReport>();
        Assert.Equal((2, 0, 0), (report!.Created, report.Updated, report.Unchanged));
        Assert.Empty(report.Errors);
        Assert.Equal(1, await CountAsync("IMP-PEPPER"));
    }

    [Fact]
    public async Task Import_SameFileTwice_DoesNotCreateDuplicates()
    {
        using var client = await CreateClientAsync();
        XLCellValue[][] lines = [["Article", "IMP-TWICE-1", "Tomate", "CA"], ["Article", "IMP-TWICE-2", "Brocoli", "CA"]];

        await client.PostAsync(Route, Form(BuildExport(lines)));
        var response = await client.PostAsync(Route, Form(BuildExport(lines)));

        var report = await response.Content.ReadFromJsonAsync<CatalogImportReport>();
        Assert.Equal((0, 0, 2), (report!.Created, report.Updated, report.Unchanged));
        Assert.Equal(1, await CountAsync("IMP-TWICE-1"));
        Assert.Equal(1, await CountAsync("IMP-TWICE-2"));
    }

    [Fact]
    public async Task Import_ReportsTheLinesOfTheReaderAndOfTheImporter_SortedByRow()
    {
        using var client = await CreateClientAsync();
        using var export = BuildExport(
            ["Article", "IMP-MIX-1", "Citron lime", "CA"],
            ["Article", "IMP-MIX-2", "", "CA"],
            ["Commentaire", "", "Livraison en retard", ""],
            ["Article", "IMP-MIX-3", "Brocoli", "CA"]);

        var response = await client.PostAsync(Route, Form(export));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<CatalogImportReport>();
        Assert.Equal(2, report!.Created);
        Assert.Equal(
            [
                new CatalogImportError(3, "IMP-MIX-2", "Name is required."),
                new CatalogImportError(4, "", "The line type is Commentaire, not Article, so it is not a food item."),
            ],
            report.Errors);
    }

    [Fact]
    public async Task Import_ReturnsBadRequest_WhenNoFileIsSent()
    {
        using var client = await CreateClientAsync();

        using var formWithoutFile = new MultipartFormDataContent { { new StringContent("PO DON1"), "note" } };

        var response = await client.PostAsync(Route, formWithoutFile);

        await AssertFileProblemAsync(response, "No file was sent. Send the Business Central export in a form field named file.");
    }

    [Fact]
    public async Task Import_ReturnsBadRequest_WhenTheFileIsNotAnXlsxFile()
    {
        using var client = await CreateClientAsync();
        using var csv = new MemoryStream("Type;N°;Description\nArticle;1000094;Citron lime"u8.ToArray());

        var response = await client.PostAsync(Route, Form(csv, "export.csv"));

        await AssertFileProblemAsync(response, "The file must be an Excel file (.xlsx).");
    }

    [Fact]
    public async Task Import_ReturnsBadRequest_WhenAnXlsxFileIsNotReallyExcel()
    {
        using var client = await CreateClientAsync();
        using var renamed = new MemoryStream("Type;N°;Description\nArticle;1000094;Citron lime"u8.ToArray());

        var response = await client.PostAsync(Route, Form(renamed, "export.xlsx"));

        await AssertFileProblemAsync(response, "The file is not a valid Excel (.xlsx) file.");
    }

    [Fact]
    public async Task Import_ReturnsBadRequest_WhenARequiredColumnIsMissing()
    {
        using var client = await CreateClientAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Lignes");
        sheet.Cell(1, 1).Value = "Type";
        sheet.Cell(1, 2).Value = "Description";
        sheet.Cell(1, 3).Value = "Code d'unité de mesure";

        var response = await client.PostAsync(Route, Form(Save(workbook)));

        await AssertFileProblemAsync(response, "The Lignes sheet is missing these columns: N°.");
    }

    [Fact]
    public async Task Import_ReturnsBadRequest_WhenTheFileIsTooLarge()
    {
        using var client = await CreateClientAsync();
        using var tooLarge = new MemoryStream(new byte[CatalogImportController.MaxFileBytes + 1]);

        var response = await client.PostAsync(Route, Form(tooLarge));

        await AssertFileProblemAsync(response, "The file is too large. The limit is 10 MB.");
    }

    private static async Task AssertFileProblemAsync(HttpResponseMessage response, string expectedDetail)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The file cannot be imported", problem!.Title);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    private async Task<HttpClient> CreateClientAsync(string role = RoleCodes.Admin)
    {
        await factory.SignInAsAsync(role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static MultipartFormDataContent Form(Stream file, string fileName = "export.xlsx")
    {
        var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { content, "file", fileName } };
    }

    private static MemoryStream BuildExport(params XLCellValue[][] lines)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Lignes");
        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = Headers[column];
        }

        for (var i = 0; i < lines.Length; i++)
        {
            for (var column = 0; column < lines[i].Length; column++)
            {
                sheet.Cell(i + 2, column + 1).Value = lines[i][column];
            }
        }

        return Save(workbook);
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private async Task<int> CountAsync(string code)
    {
        await using var context = new MoissonDbContext(
            new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(factory.ConnectionString).Options);
        return await context.CatalogItems.CountAsync(item => item.BusinessCentralCode == code);
    }
}
