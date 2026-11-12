namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Employee allowed to use the app.
/// </summary>
public class User
{
    /// <summary>
    /// Identifier of the user in the app, assigned by the database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Work email, always stored in lowercase.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Entra object id, filled when the account is linked to the employee's Microsoft account. Empty until then.
    /// </summary>
    public Guid? EntraObjectId { get; set; }

    /// <summary>
    /// Name of the employee shown in the app.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the user's role.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Role that determines what the user can see and do.
    /// </summary>
    public Role Role { get; set; } = null!;

    /// <summary>
    /// False once the admin removes the employee's access. The row is kept for history.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time the user was added, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Date and time of the last change, or null if the user was never changed.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}