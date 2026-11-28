using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Lists, creates and edits the foods in the catalog.
/// Any signed-in employee can read the catalog. Creating and editing an item requires <see cref="Permissions.ManagePriceGrid"/>.
/// </summary>
[ApiController]
[Route("api/catalog-items")]
public class CatalogItemsController(MoissonDbContext db) : ControllerBase
{
    private const string QualityClassMissing = "QualityClass must be a quality class that exists.";

    private static readonly Expression<Func<CatalogItem, CatalogItemDto>> AsDto = item => new CatalogItemDto(
        item.Id,
        item.Name,
        item.Category,
        item.BusinessCentralCode,
        item.Unit,
        item.QualityClassCode,
        item.CreatedAt,
        item.UpdatedAt);

    private static readonly Func<CatalogItem, CatalogItemDto> ToDto = AsDto.Compile();

    /// <summary>
    /// Returns every catalog item, ordered by name.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> List(CancellationToken cancellationToken)
    {
        var items = await db.CatalogItems
            .OrderBy(item => item.Name)
            .ThenBy(item => item.BusinessCentralCode)
            .Select(AsDto)
            .ToListAsync(cancellationToken);

        return items;
    }

    /// <summary>
    /// Returns one catalog item.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CatalogItemDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.CatalogItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return item is null ? NotFound() : ToDto(item);
    }

    /// <summary>
    /// Adds a catalog item. The Business Central code must be new, and the quality class must exist.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.ManagePriceGrid)]
    public async Task<ActionResult<CatalogItemDto>> Create(CatalogItemRequest request, CancellationToken cancellationToken)
    {
        var (fields, errors) = await TryReadAsync(request, cancellationToken);
        if (errors is not null)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var item = new CatalogItem
        {
            Name = fields.Name,
            Category = fields.Category,
            BusinessCentralCode = fields.BusinessCentralCode,
            Unit = fields.Unit,
            QualityClassCode = fields.QualityClass,
        };
        db.CatalogItems.Add(item);

        if (await TrySaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToDto(item));
    }

    /// <summary>
    /// Replaces the fields of a catalog item. The Business Central code must stay unique.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ManagePriceGrid)]
    public async Task<ActionResult<CatalogItemDto>> Update(Guid id, CatalogItemRequest request, CancellationToken cancellationToken)
    {
        var (fields, errors) = await TryReadAsync(request, cancellationToken);
        if (errors is not null)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var item = await db.CatalogItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Name = fields.Name;
        item.Category = fields.Category;
        item.BusinessCentralCode = fields.BusinessCentralCode;
        item.Unit = fields.Unit;
        item.QualityClassCode = fields.QualityClass;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (await TrySaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return ToDto(item);
    }

    private async Task<(CatalogItemFields Fields, Dictionary<string, string[]>? Errors)> TryReadAsync(
        CatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!CatalogItemValidator.TryNormalize(request, out var fields, out var errors))
        {
            return (fields, errors);
        }

        var qualityExists = await db.QualityClasses.AnyAsync(quality => quality.Code == fields.QualityClass, cancellationToken);
        if (!qualityExists)
        {
            return (fields, QualityClassErrors());
        }

        return (fields, null);
    }

    private static Dictionary<string, string[]> QualityClassErrors() => new()
    {
        [nameof(CatalogItemRequest.QualityClass)] = [QualityClassMissing],
    };

    private async Task<ActionResult?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (CatalogSaveErrors.IsBusinessCentralCodeConflict(exception))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Business Central code already used",
                Detail = "A catalog item with this Business Central code already exists.",
            });
        }
        catch (DbUpdateException exception) when (CatalogSaveErrors.IsMissingQualityClass(exception))
        {
            // The quality class was deleted after TryReadAsync checked it.
            return ValidationProblem(new ValidationProblemDetails(QualityClassErrors()));
        }
    }
}
