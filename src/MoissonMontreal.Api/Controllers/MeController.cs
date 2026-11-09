using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    public ActionResult<CurrentUserDto> Get() => new CurrentUserDto(
        ObjectId: User.GetObjectId(),
        Name: User.GetDisplayName(),
        Email: User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email),
        TenantId: User.GetTenantId());
}
