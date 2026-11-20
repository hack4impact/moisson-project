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
    public async Task Import_CreatesTheItemWithoutQualityClass_WhenTheRowHasNone()
    {
        var pepper = NewCode();

        var report = await ImportAsync(Row(2, pepper, name: "Poivron", quality: null));

        Assert.Equal(1, report.Created);
        Assert.Empty(report.Errors);

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.BusinessCentralCode == pepper);
        Assert.Equal(("To classify", null), (item.Category, item.QualityClassCode));
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
    public async Task Import_RowsWithTheSameCodeButDifferentNames_KeepTheLastValidRow()
    {
        var pepper = NewCode();

        var report = await ImportAsync(
            Row(2, pepper, name: "Poivron"),
            Row(3, pepper, name: "Poivron rouge"),
            Row(4, pepper, name: ""));

        Assert.Equal(1, report.Created);
        Assert.Equal(4, Assert.Single(report.Errors).RowNumber);

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.BusinessCentralCode == pepper);
        Assert.Equal(("Poivron rouge", "CA"), (item.Name, item.Unit));
    }

    [Fact]
    public async Task Import_RowsWithTheSameCodeButAnotherUnit_KeepTheUnitOfTheFirstRow_AndReportTheOthers()
    {
        var pepper = NewCode();

        var report = await ImportAsync(
            Row(2, pepper, name: "Poivron", unit: "CA"),
            Row(3, pepper, name: "Poivron", unit: "KG"),
            Row(4, pepper, name: "Poivron", unit: "ca"));

        Assert.Equal(1, report.Created);
        Assert.Equal(
            new CatalogImportError(3, pepper, "The unit KG differs from the catalog unit CA. Prices are per unit, so the item was not changed."),
            Assert.Single(report.Errors));

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.BusinessCentralCode == pepper);
        Assert.Equal("CA", item.Unit);
    }

    [Fact]
    public async Task Import_NeverChangesTheUnitOfAnExistingItem_AndReportsTheRow()
    {
        var existing = TestCatalogItems.Create("Tomate");
        await using (var setup = CreateContext())
        {
            setup.CatalogItems.Add(existing);
            await setup.SaveChangesAsync();
        }

        var report = await ImportAsync(Row(2, existing.BusinessCentralCode, name: "Tomate cerise", unit: "CA"));

        Assert.Equal((0, 0, 1), (report.Created, report.Updated, report.Unchanged));
        Assert.Equal(
            new CatalogImportError(2, existing.BusinessCentralCode, "The unit CA differs from the catalog unit kg. Prices are per unit, so the item was not changed."),
            Assert.Single(report.Errors));

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.Id == existing.Id);
        Assert.Equal(("Tomate", "kg"), (item.Name, item.Unit));
        Assert.Null(item.UpdatedAt);
    }

    [Fact]
    public async Task Import_UpdatesTheNameOfAnExistingItem_AndKeepsTheSpellingOfItsUnit()
    {
        var existing = TestCatalogItems.Create("Tomate");
        await using (var setup = CreateContext())
        {
            setup.CatalogItems.Add(existing);
            await setup.SaveChangesAsync();
        }

        // KG and kg are the same unit, so only the name changes and the unit keeps its spelling.
        var report = await ImportAsync(Row(2, existing.BusinessCentralCode, name: "Tomate cerise", unit: "KG"));

        Assert.Equal((0, 1, 0), (report.Created, report.Updated, report.Unchanged));
        Assert.Empty(report.Errors);

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.Id == existing.Id);
        Assert.Equal(("Tomate cerise", "kg"), (item.Name, item.Unit));
        Assert.NotNull(item.UpdatedAt);
    }

    [Fact]
    public async Task Import_KeepsTheCategoryAndQualityClass_OfAnExistingItem()
    {
        var existing = TestCatalogItems.Create("Brocoli");
        existing.Category = "Fruits et légumes";
        existing.QualityClassCode = "B";
        await using (var setup = CreateContext())
        {
            setup.CatalogItems.Add(existing);
            await setup.SaveChangesAsync();
        }

        var report = await ImportAsync(Row(2, existing.BusinessCentralCode, name: "Brocoli", unit: "kg", category: "To classify", quality: null));

        Assert.Equal((0, 0, 1), (report.Created, report.Updated, report.Unchanged));

        await using var context = CreateContext();
        var item = await context.CatalogItems.SingleAsync(i => i.Id == existing.Id);
        Assert.Equal(("Fruits et légumes", "B"), (item.Category, item.QualityClassCode));
        Assert.Null(item.UpdatedAt);
    }

    [Fact]
    public async Task Import_Throws_WhenTheSameCodeIsSavedMeanwhile_AndSavesNothing_ThenWorksWhenImportedAgain()
    {
        var pepper = NewCode();
        var lime = NewCode();
        CatalogImportRow[] file = [Row(2, pepper, name: "Poivron"), Row(3, lime, name: "Citron lime")];

        await using (var context = CreateContext(new SameCodeSavedMeanwhile(postgres.ConnectionString, pepper)))
        {
            await Assert.ThrowsAsync<CatalogImportConflictException>(() => new CatalogImporter(context).ImportAsync(file));
        }

        // Only the item saved by the other request exists: nothing from the file was saved.
        Assert.Equal((1, 0), (await CountAsync(pepper), await CountAsync(lime)));

        // Importing again works: the code now exists, so its line becomes an update, and the other line is created.
        var report = await ImportAsync(file);
        Assert.Equal((1, 1, 0), (report.Created, report.Updated, report.Unchanged));
        Assert.Equal((1, 1), (await CountAsync(pepper), await CountAsync(lime)));
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
        string? quality = "A",
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

    private MoissonDbContext CreateContext(SameCodeSavedMeanwhile interceptor) =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).AddInterceptors(interceptor).Options);

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}
