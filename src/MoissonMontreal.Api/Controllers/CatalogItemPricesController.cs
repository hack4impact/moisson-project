using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using MoissonMontreal.Api.Pricing;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Pricing;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Reads the price history of a catalog item and the price in effect on a given day.
/// </summary>
/// <param name="prices">Service that reads the price grid.</param>
[ApiController]
[Route("api/catalog-items/{catalogItemId:guid}/prices")]
public class CatalogItemPricesController(PriceService prices) : ControllerBase
{
    /// <summary>
    /// Returns every price the catalog item has had, oldest effective date first.
    /// </summary>
    /// <param name="catalogItemId">Identifier of the catalog item.</param>
    /// <param name="cancellationToken">Token cancelled when the client aborts the request.</param>
    /// <returns>200 with the price history (an empty list when the item has no price yet), or 404 when the item does not exist.</returns>
    [HttpGet]
    [ProducesResponseType<PriceHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceHistoryDto>> GetHistory(Guid catalogItemId, CancellationToken cancellationToken)
    {
        try
        {
            var item = await prices.GetPriceHistoryAsync(catalogItemId, cancellationToken);
            return new PriceHistoryDto(item.Id, item.Name, item.Prices.Select(ToDto).ToList());
        }
        catch (CatalogItemNotFoundException e)
        {
            return CatalogItemNotFound(e);
        }
    }

    /// <summary>
    /// Returns the price of the catalog item in effect on a day: the one with the latest effective date on or before that day.
    /// </summary>
    /// <param name="catalogItemId">Identifier of the catalog item.</param>
    /// <param name="date">Day to find the price for, in the format yyyy-MM-dd.</param>
    /// <param name="cancellationToken">Token cancelled when the client aborts the request.</param>
    /// <returns>
    /// 200 with the price, 404 when the item does not exist or has no price on that day, or 400 when the date is missing or invalid.
    /// </returns>
    [HttpGet("in-effect")]
    [ProducesResponseType<PriceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceDto>> GetPriceInEffect(
        Guid catalogItemId,
        [FromQuery, BindRequired] DateOnly date,
        CancellationToken cancellationToken)
    {
        try
        {
            return ToDto(await prices.GetPriceInEffectAsync(catalogItemId, date, cancellationToken));
        }
        catch (CatalogItemNotFoundException e)
        {
            return CatalogItemNotFound(e);
        }
        catch (PriceNotFoundException e)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext, StatusCodes.Status404NotFound, title: "No price on this date", detail: e.Message);
            problem.Extensions["catalogItemId"] = e.CatalogItemId;
            problem.Extensions["date"] = e.Date;
            problem.Extensions["firstEffectiveDate"] = e.FirstEffectiveDate;
            return NotFound(problem);
        }
    }

    private static PriceDto ToDto(Price price) => new(price.Id, price.UnitPrice, price.EffectiveDate, price.CreatedAt);

    private NotFoundObjectResult CatalogItemNotFound(CatalogItemNotFoundException e) =>
        NotFound(ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, StatusCodes.Status404NotFound, title: "Catalog item not found", detail: e.Message));
}