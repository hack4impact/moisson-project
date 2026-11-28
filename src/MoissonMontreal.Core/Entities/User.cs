namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Employee allowed to use the app.
/// </summary>
public class User
{
    /// <summary>
    /// Maximum length of <see cref="Email"/>.
    /// </summary>
    public const int EmailMaxLength = 256;

    /// <summary>
    /// Maximum length of <see cref="DisplayName"/>.
    /// </summary>
    public const int DisplayNameMaxLength = 200;

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
    /// Identifier of the admin who created the account, or null for an account inserted directly in the database, such as the first Admin.
    /// </summary>
    public int? CreatedById { get; set; }

    /// <summary>
    /// Admin who created the account, or null for an account inserted directly in the database.
    /// </summary>
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Date and time of the last change, or null if the user was never changed.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}