namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Department-based role that determines what a user can see and do.
/// </summary>
public class Role
{
    /// <summary>
    /// Fixed identifier of the role, from 1 to 4.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Stable identifier used by the permission checks.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Name of the role shown in the app.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Users who have this role.
    /// </summary>
    public ICollection<User> Users { get; set; } = [];
}