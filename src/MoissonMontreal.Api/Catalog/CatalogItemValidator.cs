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
    /// <param name="request">Fields received for the catalog item.</param>
    /// <param name="fields">Trimmed fields, ready to save when the result is true.</param>
    /// <param name="errors">Every problem found, by field. Empty when the result is true.</param>
    /// <param name="qualityClassRequired">
    /// True when a quality class must be given, as when an item is created or edited by hand. The import passes false, since
    /// imported items keep an empty class until someone picks it. A class that is given must have the right shape either way.
    /// </param>
    /// <returns>True if every field is valid.</returns>
    public static bool TryNormalize(
        CatalogItemRequest request,
        out CatalogItemFields fields,
        out Dictionary<string, string[]> errors,
        bool qualityClassRequired = true)
    {
        var found = new Dictionary<string, string[]>();
        var name = Required(request.Name, nameof(CatalogItemRequest.Name), CatalogItem.NameMaxLength, found);
        var category = Required(request.Category, nameof(CatalogItemRequest.Category), CatalogItem.CategoryMaxLength, found);
        var code = Required(request.BusinessCentralCode, nameof(CatalogItemRequest.BusinessCentralCode), CatalogItem.BusinessCentralCodeMaxLength, found);
        var unit = Required(request.Unit, nameof(CatalogItemRequest.Unit), CatalogItem.UnitMaxLength, found);
        var qualityClass = Quality(request.QualityClass, qualityClassRequired, found);

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

    /// <summary>
    /// Checks the shape of a quality class code: one capital letter, such as A, B or C.
    /// </summary>
    /// <param name="value">Quality class received, which can be null or blank.</param>
    /// <param name="required">True when an empty class is an error.</param>
    /// <param name="errors">Where a problem is added.</param>
    /// <returns>The trimmed code, or null when no class was given and none is required.</returns>
    private static string? Quality(string? value, bool required, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 && !required)
        {
            return null;
        }

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
/// Trimmed catalog fields that passed validation. QualityClass is null only when no class was given and none was required.
/// </summary>
public readonly record struct CatalogItemFields(
    string Name,
    string Category,
    string BusinessCentralCode,
    string Unit,
    string? QualityClass);
