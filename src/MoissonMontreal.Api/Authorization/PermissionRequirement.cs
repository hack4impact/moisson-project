using Microsoft.AspNetCore.Authorization;

namespace MoissonMontreal.Api.Authorization;

/// <summary>
/// Requirement of a permission policy: the employee's role must be one of the allowed roles.
/// </summary>
/// <param name="AllowedRoles">Codes of the roles allowed to use the permission.</param>
public sealed record PermissionRequirement(IReadOnlyList<string> AllowedRoles) : IAuthorizationRequirement;
