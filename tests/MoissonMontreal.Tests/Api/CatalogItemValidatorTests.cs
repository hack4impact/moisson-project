using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Tests.Api;

public class CatalogItemValidatorTests
{
    [Fact]
    public void TryNormalize_TrimsEachField()
    {
        var request = new CatalogItemRequest("  Pain  ", " Boulangerie ", " BC-001 ", " kg ", " A ");

        var ok = CatalogItemValidator.TryNormalize(request, out var fields, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Equal(new CatalogItemFields("Pain", "Boulangerie", "BC-001", "kg", "A"), fields);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("Z")]
    public void TryNormalize_AcceptsASingleCapitalLetter(string qualityClass)
    {
        var ok = CatalogItemValidator.TryNormalize(Valid() with { QualityClass = qualityClass }, out var fields, out _);

        Assert.True(ok);
        Assert.Equal(qualityClass, fields.QualityClass);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsABlankName(string? name)
    {
        var ok = CatalogItemValidator.TryNormalize(Valid() with { Name = name }, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(nameof(CatalogItemRequest.Name), errors.Keys);
    }

    [Fact]
    public void TryNormalize_RejectsANameThatIsTooLong()
    {
        var ok = CatalogItemValidator.TryNormalize(
            Valid() with { Name = new string('a', CatalogItem.NameMaxLength + 1) },
            out _,
            out var errors);

        Assert.False(ok);
        Assert.Contains(nameof(CatalogItemRequest.Name), errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsABlankCategory(string? category)
    {
        var ok = CatalogItemValidator.TryNormalize(Valid() with { Category = category }, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(nameof(CatalogItemRequest.Category), errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsABlankBusinessCentralCode(string? code)
    {
        var ok = CatalogItemValidator.TryNormalize(Valid() with { BusinessCentralCode = code }, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(nameof(CatalogItemRequest.BusinessCentralCode), errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsABlankUnit(string? unit)
    {
        var ok = CatalogItemValidator.TryNormalize(Valid() with { Unit = unit }, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(nameof(CatalogItemRequest.Unit), errors.Keys);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("AB")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormalize_RejectsAQualityClassThatIsNotOneCapitalLetter(string? qualityClass)
    {
        var ok = CatalogItemValidator.TryNormalize(Valid() with { QualityClass = qualityClass }, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(nameof(CatalogItemRequest.QualityClass), errors.Keys);
    }

    private static CatalogItemRequest Valid() => new("Pain", "Boulangerie", "BC-001", "kg", "A");
}
