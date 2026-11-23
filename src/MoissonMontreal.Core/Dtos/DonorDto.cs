namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// A donor returned by the API.
/// </summary>
public record DonorDto(
    Guid Id,
    string Name,
    string Type,
    string? Address,
    string? City,
    string? Province,
    string? PostalCode,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Notes,
    string? BusinessCentralCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
