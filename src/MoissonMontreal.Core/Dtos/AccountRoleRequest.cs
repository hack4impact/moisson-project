namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when an administrator gives an account another role.
/// </summary>
/// <param name="RoleCode">Code of the new role.</param>
/// <param name="Version">Version of the account the administrator loaded.</param>
public record AccountRoleRequest(string? RoleCode, uint? Version);
