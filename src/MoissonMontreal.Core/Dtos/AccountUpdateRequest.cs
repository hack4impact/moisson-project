namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when an administrator edits the name and the email of an account.
/// </summary>
/// <param name="Email">New work email.</param>
/// <param name="DisplayName">New name.</param>
/// <param name="Version">Version of the account the administrator loaded.</param>
public record AccountUpdateRequest(string? Email, string? DisplayName, uint? Version);
