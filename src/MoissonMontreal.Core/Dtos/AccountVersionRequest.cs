namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Field sent when an administrator removes or gives back an employee's access.
/// </summary>
/// <param name="Version">Version of the account the administrator loaded.</param>
public record AccountVersionRequest(uint? Version);
