namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when an administrator adds an employee.
/// </summary>
/// <param name="Email">Work email of the employee.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="RoleCode">Code of the role, such as Supply.</param>
public record AccountCreateRequest(string? Email, string? DisplayName, string? RoleCode);
