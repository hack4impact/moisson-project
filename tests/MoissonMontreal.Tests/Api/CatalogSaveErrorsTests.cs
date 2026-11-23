using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Api.Data.Configurations;
using Npgsql;

namespace MoissonMontreal.Tests.Api;

public class CatalogSaveErrorsTests
{
    [Fact]
    public void RecognizesADuplicateBusinessCentralCode()
    {
        var exception = RefusedBy(CatalogItemConfiguration.BusinessCentralCodeIndex);

        Assert.True(CatalogSaveErrors.IsBusinessCentralCodeConflict(exception));
        Assert.False(CatalogSaveErrors.IsMissingQualityClass(exception));
    }

    [Fact]
    public void RecognizesAQualityClassThatDoesNotExist()
    {
        var exception = RefusedBy(CatalogItemConfiguration.QualityClassForeignKey);

        Assert.True(CatalogSaveErrors.IsMissingQualityClass(exception));
        Assert.False(CatalogSaveErrors.IsBusinessCentralCodeConflict(exception));
    }

    [Fact]
    public void RecognizesNeither_WhenAnotherConstraintRefusedTheSave()
    {
        var exception = RefusedBy("ck_catalog_items_name_not_empty");

        Assert.False(CatalogSaveErrors.IsBusinessCentralCodeConflict(exception));
        Assert.False(CatalogSaveErrors.IsMissingQualityClass(exception));
    }

    [Fact]
    public void RecognizesNeither_WhenTheErrorDoesNotComeFromPostgreSql()
    {
        var exception = new DbUpdateException("The save failed.", new TimeoutException());

        Assert.False(CatalogSaveErrors.IsBusinessCentralCodeConflict(exception));
        Assert.False(CatalogSaveErrors.IsMissingQualityClass(exception));
    }

    [Fact]
    public void RecognizesNeither_WhenTheErrorHasNoCause()
    {
        var exception = new DbUpdateException("The save failed.");

        Assert.False(CatalogSaveErrors.IsBusinessCentralCodeConflict(exception));
        Assert.False(CatalogSaveErrors.IsMissingQualityClass(exception));
    }

    private static DbUpdateException RefusedBy(string constraintName) =>
        new("The save failed.", new PostgresException("refused", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: constraintName));
}
