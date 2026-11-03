namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Department-based role that determines what a user can see and do.
/// </summary>
public class Role
{
    public int Id { get; set; }

    /// <summary>
    /// Stable identifier used by the permission checks.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = [];
}