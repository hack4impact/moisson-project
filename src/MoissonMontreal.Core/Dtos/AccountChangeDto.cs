namespace MoissonMontreal.Core.Dtos;

/// <summary>
/// One change in the history of an account. Action is Created, Updated, RoleChanged, Deactivated or Reactivated.
/// Only the values the change touched are filled; the others are null.
/// </summary>
/// <param name="Id">Identifier of the change.</param>
/// <param name="Action">Kind of change.</param>
/// <param name="OldEmail">Email before the change.</param>
/// <param name="NewEmail">Email after the change.</param>
/// <param name="OldDisplayName">Name before the change.</param>
/// <param name="NewDisplayName">Name after the change.</param>
/// <param name="OldRoleCode">Role before the change.</param>
/// <param name="NewRoleCode">Role after the change.</param>
/// <param name="OldIsActive">Access before the change.</param>
/// <param name="NewIsActive">Access after the change.</param>
/// <param name="ChangedByUserId">Administrator who made the change.</param>
/// <param name="ChangedByName">Name of the administrator who made the change.</param>
/// <param name="ChangedAt">Date and time of the change.</param>
public record AccountChangeDto(
    Guid Id,
    string Action,
    string? OldEmail,
    string? NewEmail,
    string? OldDisplayName,
    string? NewDisplayName,
    string? OldRoleCode,
    string? NewRoleCode,
    bool? OldIsActive,
    bool? NewIsActive,
    int ChangedByUserId,
    string ChangedByName,
    DateTimeOffset ChangedAt);
