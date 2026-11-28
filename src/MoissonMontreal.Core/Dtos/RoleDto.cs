namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// A role an administrator can give to an account.
/// </summary>
/// <param name="Code">Code of the role, sent back when the role is chosen.</param>
/// <param name="Name">Name of the role shown on screen.</param>
public record RoleDto(string Code, string Name);
