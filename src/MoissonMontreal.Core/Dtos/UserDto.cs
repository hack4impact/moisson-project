namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// An account returned by the API. Role is the role code. CreatedById is null for an account inserted directly in the database, such as the first Admin.
/// </summary>
public record UserDto(
    int Id,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int? CreatedById);
