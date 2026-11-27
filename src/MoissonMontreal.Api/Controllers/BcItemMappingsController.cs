using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Authorization;
using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Catalog;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Lists, confirms, changes and deletes the mappings between Business Central item codes and catalog items.
/// Any signed-in employee can read them. Changing them requires <see cref="Permissions.ManageDonations"/>,
/// because matching items is part of importing donations (PRD, Donations section).
/// </summary>
/// <param name="db">Database context of the request.</param>
/// <param name="mappings">Service that saves the mappings with their history.</param>
/// <param name="userLookup">Service that finds the app user of the signed-in employee.</param>
[ApiController]
[Route("api/bc-item-mappings")]
public class BcItemMappingsController(
    MoissonDbContext db,
    BcItemMappingService mappings,
    UserLookupService userLookup) : ControllerBase
{
    /// <summary>
    /// Turns a mapping into the shape returned by the API, inside the database query.
    /// </summary>
    private static readonly Expression<Func<BcItemMapping, BcItemMappingDto>> AsDto = m => new BcItemMappingDto(
        m.Id,
        m.BusinessCentralCode,
        m.CatalogItemId,
        m.CatalogItem.Name,
        m.CatalogItem.Unit,
        m.CatalogItem.Category,
        m.CatalogItem.QualityClassCode,
        !m.CatalogItem.IsActive,
        m.ConfirmedByUserId,
        m.ConfirmedBy.DisplayName,
        m.ConfirmedAt);

    /// <summary>
    /// Lists the mappings, sorted by code.
    /// </summary>
    /// <param name="needsReview">When true, only the mappings whose catalog item is no longer active.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The mappings.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BcItemMappingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BcItemMappingDto>>> List(
        [FromQuery] bool needsReview,
        CancellationToken cancellationToken)
    {
        var query = db.BcItemMappings.AsQueryable();
        if (needsReview)
        {
            query = query.Where(m => !m.CatalogItem.IsActive);
        }

        return await query
            .OrderBy(m => m.BusinessCentralCode)
            .Select(AsDto)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the mapping of one code.
    /// </summary>
    /// <param name="code">Business Central item code.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The mapping, or 404 when the code has none.</returns>
    [HttpGet("{code}")]
    [ProducesResponseType<BcItemMappingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BcItemMappingDto>> Get(string code, CancellationToken cancellationToken)
    {
        var mapping = await FindDtoAsync(code.Trim(), cancellationToken);
        return mapping is null ? MappingNotFound(new BcItemMappingNotFoundException(code.Trim())) : mapping;
    }

    /// <summary>
    /// Returns every change made to the mapping of one code, newest first. The history stays after the mapping is deleted.
    /// </summary>
    /// <param name="code">Business Central item code.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The changes, or an empty list when the code never had a mapping.</returns>
    [HttpGet("{code}/history")]
    [ProducesResponseType<IReadOnlyList<BcItemMappingChangeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BcItemMappingChangeDto>>> GetHistory(string code, CancellationToken cancellationToken)
    {
        var trimmed = code.Trim();
        return await db.BcItemMappingChanges
            .Where(c => c.BusinessCentralCode == trimmed)
            .OrderByDescending(c => c.ChangedAt)
            .Select(c => new BcItemMappingChangeDto(
                c.Id,
                c.BusinessCentralCode,
                c.Action.ToString(),
                c.OldCatalogItemId,
                c.NewCatalogItemId,
                c.ChangedByUserId,
                c.ChangedBy.DisplayName,
                c.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Saves a mapping confirmed by the signed-in employee.
    /// </summary>
    /// <param name="request">Code and catalog item to match.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>201 with the mapping, 400 when a field is missing or the item is inactive, 404 when the item does not exist,
    /// or 409 when the code already has a mapping.</returns>
    [HttpPost]
    [Authorize(Policy = Permissions.ManageDonations)]
    [ProducesResponseType<BcItemMappingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BcItemMappingDto>> Create(BcItemMappingCreateRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.BusinessCentralCode))
        {
            errors[nameof(request.BusinessCentralCode)] = ["BusinessCentralCode is required."];
        }
        else if (request.BusinessCentralCode.Trim().Length > CatalogItem.BusinessCentralCodeMaxLength)
        {
            errors[nameof(request.BusinessCentralCode)] =
                [$"BusinessCentralCode must be at most {CatalogItem.BusinessCentralCodeMaxLength} characters."];
        }

        if (request.CatalogItemId is null)
        {
            errors[nameof(request.CatalogItemId)] = ["CatalogItemId is required."];
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        if (await userLookup.FindActiveUserAsync(User, cancellationToken) is not { } user)
        {
            return Forbid();
        }

        try
        {
            var mapping = await mappings.CreateAsync(request.BusinessCentralCode!, request.CatalogItemId!.Value, user.Id, cancellationToken);
            return CreatedAtAction(nameof(Get), new { code = mapping.BusinessCentralCode }, ToDto(mapping, user));
        }
        catch (CatalogItemNotFoundException e)
        {
            return CatalogItemNotFound(e);
        }
        catch (CatalogItemInactiveException e)
        {
            return CatalogItemInactive(e);
        }
        catch (BcItemMappingConflictException e)
        {
            return Conflict(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext, StatusCodes.Status409Conflict, title: "Business Central code already mapped", detail: e.Message));
        }
    }

    /// <summary>
    /// Points the mapping of a code to another catalog item, confirmed by the signed-in employee.
    /// </summary>
    /// <param name="code">Business Central item code.</param>
    /// <param name="request">Catalog item now chosen.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>200 with the mapping, 400 when the item is missing or inactive, or 404 when the code or the item does not exist.</returns>
    [HttpPut("{code}")]
    [Authorize(Policy = Permissions.ManageDonations)]
    [ProducesResponseType<BcItemMappingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BcItemMappingDto>> Update(
        string code,
        BcItemMappingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CatalogItemId is null)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.CatalogItemId)] = ["CatalogItemId is required."],
            }));
        }

        if (await userLookup.FindActiveUserAsync(User, cancellationToken) is not { } user)
        {
            return Forbid();
        }

        try
        {
            var mapping = await mappings.UpdateAsync(code, request.CatalogItemId.Value, user.Id, cancellationToken);
            return ToDto(mapping, user);
        }
        catch (BcItemMappingNotFoundException e)
        {
            return MappingNotFound(e);
        }
        catch (CatalogItemNotFoundException e)
        {
            return CatalogItemNotFound(e);
        }
        catch (CatalogItemInactiveException e)
        {
            return CatalogItemInactive(e);
        }
    }

    /// <summary>
    /// Deletes the mapping of a code, so the next import asks a person again. The history keeps the deletion.
    /// </summary>
    /// <param name="code">Business Central item code.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>204, or 404 when the code has no mapping.</returns>
    [HttpDelete("{code}")]
    [Authorize(Policy = Permissions.ManageDonations)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        if (await userLookup.FindActiveUserAsync(User, cancellationToken) is not { } user)
        {
            return Forbid();
        }

        try
        {
            await mappings.DeleteAsync(code, user.Id, cancellationToken);
            return NoContent();
        }
        catch (BcItemMappingNotFoundException e)
        {
            return MappingNotFound(e);
        }
    }

    /// <summary>
    /// Loads the mapping of a code in the shape returned by the API.
    /// </summary>
    /// <param name="code">Code without spaces around it.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The mapping, or null when the code has none.</returns>
    private Task<BcItemMappingDto?> FindDtoAsync(string code, CancellationToken cancellationToken) =>
        db.BcItemMappings
            .Where(m => m.BusinessCentralCode == code)
            .Select(AsDto)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Builds the API shape of a mapping the signed-in employee just saved.
    /// </summary>
    /// <param name="mapping">Saved mapping, with its catalog item.</param>
    /// <param name="user">Employee who confirmed it.</param>
    /// <returns>The mapping as returned by the API.</returns>
    private static BcItemMappingDto ToDto(BcItemMapping mapping, User user) => new(
        mapping.Id,
        mapping.BusinessCentralCode,
        mapping.CatalogItemId,
        mapping.CatalogItem.Name,
        mapping.CatalogItem.Unit,
        mapping.CatalogItem.Category,
        mapping.CatalogItem.QualityClassCode,
        mapping.NeedsReview,
        user.Id,
        user.DisplayName,
        mapping.ConfirmedAt);

    /// <summary>
    /// Builds the 404 answer for a code that has no mapping.
    /// </summary>
    /// <param name="e">Error thrown by the service.</param>
    /// <returns>A 404 with the code.</returns>
    private NotFoundObjectResult MappingNotFound(BcItemMappingNotFoundException e)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, StatusCodes.Status404NotFound, title: "Mapping not found", detail: e.Message);
        problem.Extensions["businessCentralCode"] = e.BusinessCentralCode;
        return NotFound(problem);
    }

    /// <summary>
    /// Builds the 404 answer for a catalog item that does not exist.
    /// </summary>
    /// <param name="e">Error thrown by the service.</param>
    /// <returns>A 404 with the catalog item identifier.</returns>
    private NotFoundObjectResult CatalogItemNotFound(CatalogItemNotFoundException e)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, StatusCodes.Status404NotFound, title: "Catalog item not found", detail: e.Message);
        problem.Extensions["catalogItemId"] = e.CatalogItemId;
        return NotFound(problem);
    }

    /// <summary>
    /// Builds the 400 answer for a catalog item removed from the price grid.
    /// </summary>
    /// <param name="e">Error thrown by the service.</param>
    /// <returns>A 400 that tells the person to pick an active item.</returns>
    private ActionResult CatalogItemInactive(CatalogItemInactiveException e) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["CatalogItemId"] = [$"{e.Message} Pick an active catalog item."],
        }));
}
