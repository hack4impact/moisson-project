using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using MoissonMontreal.Core.Authorization;

namespace MoissonMontreal.Api.Authorization;

/// <summary>
/// Registers one authorization policy per permission. The role is read from the app's database by <see cref="PermissionHandler"/>.
/// </summary>
public static class PermissionPolicies
{
    /// <summary>
    /// Adds a policy for each permission in <see cref="Permissions.AllowedRoles"/>.
    /// Each policy requires a valid token with the <c>access_as_user</c> scope and one of the allowed roles.
    /// </summary>
    /// <param name="builder">Authorization builder from <c>Program.cs</c>.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var (permission, allowedRoles) in Permissions.AllowedRoles)
        {
            builder.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireScope("access_as_user")
                .AddRequirements(new PermissionRequirement(allowedRoles)));
        }

        return builder;
    }
}
