using Microsoft.AspNetCore.Authorization;

namespace MoissonMontreal.Api.Authorization;

/// <summary>
/// Checks a <see cref="PermissionRequirement"/> by reading the employee's role from the users table.
/// Roles sent by Microsoft in the token are never used.
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// Finds the employee's account and role.
    /// </summary>
    private readonly UserLookupService _userLookup;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="userLookup">Service that finds the employee's account in the users table.</param>
    public PermissionHandler(UserLookupService userLookup)
    {
        _userLookup = userLookup;
    }

    /// <summary>
    /// Succeeds when the employee has an active account whose role is allowed. Otherwise the request gets a 403.
    /// </summary>
    /// <param name="context">Information about the request being authorized, including the signed-in employee.</param>
    /// <param name="requirement">Roles allowed by the permission.</param>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var user = await _userLookup.FindActiveUserAsync(context.User, cancellationToken);
        if (user is not null && requirement.AllowedRoles.Contains(user.Role.Code))
        {
            context.Succeed(requirement);
        }
    }
}
