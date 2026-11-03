namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Employee allowed to use the app.
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>
    /// Work email, always stored in lowercase.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Entra object id, filled on the employee's first sign-in.
    /// </summary>
    public Guid? EntraObjectId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public Role Role { get; set; } = null!;

    /// <summary>
    /// False once the admin removes the employee's access. The row is kept for history.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}