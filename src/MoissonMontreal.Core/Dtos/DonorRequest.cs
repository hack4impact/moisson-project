namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// Fields sent when a donor is created or edited.
/// </summary>
public record DonorRequest(
    string? Name,
    string? Type,
    string? Address,
    string? City,
    string? Province,
    string? PostalCode,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Notes,
    string? BusinessCentralCode);
