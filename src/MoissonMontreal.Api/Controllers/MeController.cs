using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MoissonMontreal.Api.Authorization;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Tells the desktop app who is signed in and with which role, so it can show only what the employee is allowed to use.
/// Like every route, it refuses a deactivated or never-added account.
/// </summary>
/// <param name="userLookup">Service that finds the app account of the signed-in employee.</param>
[ApiController]
[Route("api/me")]
public class MeController(UserLookupService userLookup) : ControllerBase
{
    /// <summary>
    /// Returns the signed-in employee and their role.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The employee, or 403 when they have no active account.</returns>
    [HttpGet]
    public async Task<ActionResult<CurrentUserDto>> Get(CancellationToken cancellationToken)
    {
        if (await userLookup.FindActiveUserAsync(User, cancellationToken) is not { } user)
        {
            return Forbid();
        }

        return new CurrentUserDto(
            ObjectId: User.FindFirstValue("oid"),
            Name: User.FindFirstValue("name"),
            Email: User.FindFirstValue("email") ?? User.FindFirstValue("preferred_username"),
            TenantId: User.FindFirstValue("tid"),
            Role: user.Role.Code);
    }
}
