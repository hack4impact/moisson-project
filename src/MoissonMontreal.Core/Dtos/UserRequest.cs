namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when an admin creates an account. Role is one of the codes in <see cref="Authorization.RoleCodes"/>.
/// </summary>
public record UserRequest(
    string? DisplayName,
    string? Email,
    string? Role);
