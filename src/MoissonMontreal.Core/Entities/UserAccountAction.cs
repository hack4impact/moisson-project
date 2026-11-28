namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Kind of change an administrator made to an employee's account.
/// </summary>
public enum UserAccountAction
{
    /// <summary>
    /// The employee was added with a work email, a name and a role.
    /// </summary>
    Created,

    /// <summary>
    /// The name or the email of the account was edited.
    /// </summary>
    Updated,

    /// <summary>
    /// The employee was given another role.
    /// </summary>
    RoleChanged,

    /// <summary>
    /// The employee's access was removed. The account is kept so the history stays attributed to them.
    /// </summary>
    Deactivated,

    /// <summary>
    /// The employee's access was given back.
    /// </summary>
    Reactivated
}
