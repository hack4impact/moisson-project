using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using Npgsql;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Lists, creates and edits the foods in the catalog.
/// </summary>
[ApiController]
[Route("api/catalog-items")]
public class CatalogItemsController(MoissonDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns every catalog item, ordered by name.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> List(CancellationToken cancellationToken)
    {
        var items = await db.CatalogItems
            .OrderBy(item => item.Name)
            .ThenBy(item => item.BusinessCentralCode)
            .Select(item => new CatalogItemDto(
                item.Id,
                item.Name,
                item.Category,
                item.BusinessCentralCode,
                item.Unit,
                item.QualityClassCode,
                item.CreatedAt,
                item.UpdatedAt))
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
    public async Task<ActionResult<CatalogItemDto>> Create(CatalogItemRequest request, CancellationToken cancellationToken)
    {
        var fields = await TryReadAsync(request, cancellationToken);
        if (fields is null)
        {
            return ValidationProblem(new ValidationProblemDetails(LastErrors));
        }

        var item = new CatalogItem
        {
            Name = fields.Value.Name,
            Category = fields.Value.Category,
            BusinessCentralCode = fields.Value.BusinessCentralCode,
            Unit = fields.Value.Unit,
            QualityClassCode = fields.Value.QualityClass,
        };
        db.CatalogItems.Add(item);

        if (await SaveConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToDto(item));
    }

    /// <summary>
    /// Replaces the fields of a catalog item. The Business Central code must stay unique.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CatalogItemDto>> Update(Guid id, CatalogItemRequest request, CancellationToken cancellationToken)
    {
        var fields = await TryReadAsync(request, cancellationToken);
        if (fields is null)
        {
            return ValidationProblem(new ValidationProblemDetails(LastErrors));
        }

        var item = await db.CatalogItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Name = fields.Value.Name;
        item.Category = fields.Value.Category;
        item.BusinessCentralCode = fields.Value.BusinessCentralCode;
        item.Unit = fields.Value.Unit;
        item.QualityClassCode = fields.Value.QualityClass;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (await SaveConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return ToDto(item);
    }

    private Dictionary<string, string[]> LastErrors { get; set; } = [];

    private async Task<CatalogItemFields?> TryReadAsync(CatalogItemRequest request, CancellationToken cancellationToken)
    {
        if (!CatalogItemValidator.TryNormalize(request, out var fields, out var errors))
        {
            LastErrors = errors;
            return null;
        }

        var qualityExists = await db.QualityClasses.AnyAsync(quality => quality.Code == fields.QualityClass, cancellationToken);
        if (!qualityExists)
        {
            LastErrors = new Dictionary<string, string[]>
            {
                [nameof(CatalogItemRequest.QualityClass)] = ["QualityClass must be a quality class that exists."],
            };
            return null;
        }

        return fields;
    }

    private async Task<ActionResult?> SaveConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.ConstraintName == CatalogItemConfiguration.BusinessCentralCodeIndex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Business Central code already used",
                Detail = "A catalog item with this Business Central code already exists.",
            });
        }
    }

    private static CatalogItemDto ToDto(CatalogItem item) => new(
        item.Id,
        item.Name,
        item.Category,
        item.BusinessCentralCode,
        item.Unit,
        item.QualityClassCode,
        item.CreatedAt,
        item.UpdatedAt);
}
