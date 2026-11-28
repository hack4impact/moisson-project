namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// The signed-in employee, as returned by <c>GET /api/me</c>: who they are in Microsoft Entra ID, and their role in the app.
/// </summary>
/// <param name="ObjectId">Identifier of the employee's Microsoft account (the <c>oid</c> claim).</param>
/// <param name="Name">Name from the Microsoft account.</param>
/// <param name="Email">Work email, or the Microsoft username when the token has no email.</param>
/// <param name="TenantId">Identifier of the Moisson Montréal tenant.</param>
/// <param name="Role">Code of the employee's role in the app, such as Admin. It comes from the users table, not from Entra ID.</param>
public record CurrentUserDto(string? ObjectId, string? Name, string? Email, string? TenantId, string Role);
