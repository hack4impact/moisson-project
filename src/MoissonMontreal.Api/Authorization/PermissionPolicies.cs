using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using MoissonMontreal.Core.Authorization;

namespace MoissonMontreal.Api.Authorization;

/// <summary>
/// Registers one authorization policy per permission, based on the role stored in the app's database.
/// </summary>
public static class PermissionPolicies
{
    /// <summary>
    /// Type of the claim that holds the user's role code from the users table.
    /// A custom name is used so that roles sent by Microsoft in the token (<c>roles</c>) are never trusted.
    /// </summary>
    public const string RoleClaimType = "moisson_role";

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
                .RequireClaim(RoleClaimType, allowedRoles));
        }

        return builder;
    }
}
