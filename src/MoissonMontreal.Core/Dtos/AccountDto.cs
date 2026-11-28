namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// An employee's account returned by the API. Version must be sent back with every change, so a change made by another
/// administrator in the meantime is detected instead of overwritten.
/// </summary>
/// <param name="Id">Identifier of the account.</param>
/// <param name="Email">Work email, in lowercase.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="RoleCode">Code of the role, such as Admin.</param>
/// <param name="RoleName">Name of the role shown on screen.</param>
/// <param name="IsActive">False once the employee's access was removed.</param>
/// <param name="IsLinked">True once the employee has signed in and their Microsoft account is linked.</param>
/// <param name="CreatedAt">Date and time the account was added.</param>
/// <param name="UpdatedAt">Date and time of the last change, or null if never changed.</param>
/// <param name="Version">Version of the account, to send back with the next change.</param>
public record AccountDto(
    int Id,
    string Email,
    string DisplayName,
    string RoleCode,
    string RoleName,
    bool IsActive,
    bool IsLinked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint Version);
