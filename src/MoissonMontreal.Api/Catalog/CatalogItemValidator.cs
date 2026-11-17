using System.Text.RegularExpressions;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Checks a catalog item request and returns the values ready to store.
/// </summary>
public static partial class CatalogItemValidator
{
    /// <summary>
    /// Trims each field and reports every problem. A true result means <paramref name="fields"/> can be saved.
    /// The quality class must also exist in the quality_classes table; this method only checks its shape.
    /// </summary>
    public static bool TryNormalize(CatalogItemRequest request, out CatalogItemFields fields, out Dictionary<string, string[]> errors)
    {
        var found = new Dictionary<string, string[]>();
        var name = Required(request.Name, nameof(CatalogItemRequest.Name), CatalogItem.NameMaxLength, found);
        var category = Required(request.Category, nameof(CatalogItemRequest.Category), CatalogItem.CategoryMaxLength, found);
        var code = Required(request.BusinessCentralCode, nameof(CatalogItemRequest.BusinessCentralCode), CatalogItem.BusinessCentralCodeMaxLength, found);
        var unit = Required(request.Unit, nameof(CatalogItemRequest.Unit), CatalogItem.UnitMaxLength, found);
        var qualityClass = Quality(request.QualityClass, found);

        fields = new CatalogItemFields(name, category, code, unit, qualityClass);
        errors = found;
        return found.Count == 0;
    }

    private static string Required(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors[field] = [$"{field} is required."];
        }
        else if (trimmed.Length > maxLength)
        {
            errors[field] = [$"{field} must be {maxLength} characters or fewer."];
        }

        return trimmed;
    }

    private static string Quality(string? value, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!QualityClassCode().IsMatch(trimmed))
        {
            errors[nameof(CatalogItemRequest.QualityClass)] = ["QualityClass must be one capital letter, such as A, B or C."];
        }

        return trimmed;
    }

    [GeneratedRegex("^[A-Z]$")]
    private static partial Regex QualityClassCode();
}

/// <summary>
/// Trimmed catalog fields that passed validation.
/// </summary>
public readonly record struct CatalogItemFields(
    string Name,
    string Category,
    string BusinessCentralCode,
    string Unit,
    string QualityClass);
