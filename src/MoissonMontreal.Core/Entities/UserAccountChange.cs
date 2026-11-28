namespace MoissonMontreal.Core.Entities;

/// <summary>
/// One change an administrator made to an employee's account, kept for history with its before and after values.
/// Only the values the action touched are filled: for example, a deactivation only fills the active flags.
/// </summary>
public class UserAccountChange
{
    /// <summary>
    /// Identifier of the change, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the account that was changed.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Account that was changed.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Kind of change.
    /// </summary>
    public UserAccountAction Action { get; set; }

    /// <summary>
    /// Email before the change, or null when the action did not touch the email.
    /// </summary>
    public string? OldEmail { get; set; }

    /// <summary>
    /// Email after the change, or null when the action did not touch the email.
    /// </summary>
    public string? NewEmail { get; set; }

    /// <summary>
    /// Name before the change, or null when the action did not touch the name.
    /// </summary>
    public string? OldDisplayName { get; set; }

    /// <summary>
    /// Name after the change, or null when the action did not touch the name.
    /// </summary>
    public string? NewDisplayName { get; set; }

    /// <summary>
    /// Identifier of the role before the change, or null when the action did not touch the role.
    /// </summary>
    public int? OldRoleId { get; set; }

    /// <summary>
    /// Identifier of the role after the change, or null when the action did not touch the role.
    /// </summary>
    public int? NewRoleId { get; set; }

    /// <summary>
    /// Whether the account was active before the change, or null when the action did not touch it.
    /// </summary>
    public bool? OldIsActive { get; set; }

    /// <summary>
    /// Whether the account is active after the change, or null when the action did not touch it.
    /// </summary>
    public bool? NewIsActive { get; set; }

    /// <summary>
    /// Identifier of the administrator who made the change.
    /// </summary>
    public int ChangedByUserId { get; set; }

    /// <summary>
    /// Administrator who made the change.
    /// </summary>
    public User ChangedBy { get; set; } = null!;

    /// <summary>
    /// Date and time of the change, set by the database.
    /// </summary>
    public DateTimeOffset ChangedAt { get; set; }
}
