namespace MoissonMontreal.Core.Accounts;

/// <summary>
/// Thrown when an account is given a role code that matches no role.
/// </summary>
/// <param name="roleCode">Code that matches no role.</param>
public class RoleNotFoundException(string roleCode) : Exception($"Role {roleCode} does not exist.")
{
    /// <summary>
    /// Code that matches no role.
    /// </summary>
    public string RoleCode { get; } = roleCode;
}
