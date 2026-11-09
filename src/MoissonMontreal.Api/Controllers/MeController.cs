using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    public ActionResult<CurrentUserDto> Get() => new CurrentUserDto(
        ObjectId: User.FindFirstValue("oid"),
        Name: User.FindFirstValue("name"),
        Email: User.FindFirstValue("email") ?? User.FindFirstValue("preferred_username"),
        TenantId: User.FindFirstValue("tid"));
}
