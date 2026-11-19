using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class CatalogImporterTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Import_CreatesOneItemPerNewCode()
    {
        var lime = NewCode();
        var pepper = NewCode();

        var report = await ImportAsync(
            Row(2, lime, name: "Citron lime"),
            Row(3, pepper, name: "Poivron"));

        Assert.Equal((2, 0, 0), (report.Created, report.Updated, report.Unchanged));
        Assert.Empty(report.Errors);

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.BusinessCentralCode == lime);
        Assert.Equal(("Citron lime", "To classify", "CA", "A"), (item.Name, item.Category, item.Unit, item.QualityClassCode));
    }

    [Fact]
    public async Task Import_SameFileTwice_DoesNotCreateDuplicates()
    {
        var lime = NewCode();
        var pepper = NewCode();
        CatalogImportRow[] file = [Row(2, lime), Row(3, pepper)];

        await ImportAsync(file);
        var second = await ImportAsync(file);

        Assert.Equal((0, 0, 2), (second.Created, second.Updated, second.Unchanged));
        Assert.Equal(1, await CountAsync(lime));
        Assert.Equal(1, await CountAsync(pepper));
    }

    [Fact]
    public async Task Import_RowsWithTheSameCode_CreateOneItem()
    {
        var pepper = NewCode();
        var rows = Enumerable.Range(2, 8).Select(rowNumber => Row(rowNumber, pepper, name: "Poivron")).ToArray();

        var report = await ImportAsync(rows);

        Assert.Equal((1, 0, 0), (report.Created, report.Updated, report.Unchanged));
        Assert.Equal(1, await CountAsync(pepper));
    }

    [Fact]
    public async Task Import_UpdatesAnExistingItem_WhenItsValuesChanged()
    {
        var existing = TestCatalogItems.Create("Tomate");
        await using (var setup = CreateContext())
        {
            setup.CatalogItems.Add(existing);
            await setup.SaveChangesAsync();
        }

        var report = await ImportAsync(Row(2, existing.BusinessCentralCode, name: "Tomate cerise", unit: "KG"));

        Assert.Equal((0, 1, 0), (report.Created, report.Updated, report.Unchanged));

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.Id == existing.Id);
        Assert.Equal(("Tomate cerise", "KG"), (item.Name, item.Unit));
        Assert.NotNull(item.UpdatedAt);
    }

    [Fact]
    public async Task Import_ReportsAnInvalidRow_AndImportsTheOthers()
    {
        var lime = NewCode();
        var broken = NewCode();
        var pepper = NewCode();

        var report = await ImportAsync(
            Row(2, lime),
            Row(3, broken, name: "   "),
            Row(4, pepper));

        Assert.Equal(2, report.Created);
        var error = Assert.Single(report.Errors);
        Assert.Equal(new CatalogImportError(3, broken, "Name is required."), error);
        Assert.Equal(0, await CountAsync(broken));
    }

    [Fact]
    public async Task Import_ReportsEveryProblemOfARow_InOneMessage()
    {
        var code = NewCode();

        var report = await ImportAsync(Row(5, code, name: "", unit: ""));

        var error = Assert.Single(report.Errors);
        Assert.Equal((5, code), (error.RowNumber, error.BusinessCentralCode));
        Assert.Contains("Name is required.", error.Message);
        Assert.Contains("Unit is required.", error.Message);
    }

    [Fact]
    public async Task Import_ReportsARow_WhenTheBusinessCentralCodeIsEmpty()
    {
        var report = await ImportAsync(Row(7, ""));

        Assert.Equal(new CatalogImportError(7, "", "BusinessCentralCode is required."), Assert.Single(report.Errors));
        Assert.Equal(0, report.Created);
    }

    [Fact]
    public async Task Import_ReportsARow_WhenTheQualityClassDoesNotExist()
    {
        var code = NewCode();

        var report = await ImportAsync(Row(2, code, quality: "Z"));

        Assert.Equal(new CatalogImportError(2, code, "Quality class Z does not exist."), Assert.Single(report.Errors));
        Assert.Equal(0, await CountAsync(code));
    }

    private static string NewCode() => "TEST-" + Guid.NewGuid().ToString("N")[..12];

    private static CatalogImportRow Row(
        int rowNumber,
        string code,
        string name = "Poivron",
        string unit = "CA",
        string quality = "A",
        string category = "To classify") =>
        new(rowNumber, new CatalogItemRequest(name, category, code, unit, quality));

    private async Task<CatalogImportReport> ImportAsync(params CatalogImportRow[] rows)
    {
        await using var context = CreateContext();
        return await new CatalogImporter(context).ImportAsync(rows);
    }

    private async Task<int> CountAsync(string code)
    {
        await using var context = CreateContext();
        return await context.CatalogItems.CountAsync(item => item.BusinessCentralCode == code);
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}
