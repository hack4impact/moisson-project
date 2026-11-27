namespace MoissonMontreal.Core.Accounts;

/// <summary>
/// Thrown when an administrator tries to change their own role or remove their own access,
/// so they cannot lock themselves out by mistake. Another administrator has to do it.
/// </summary>
/// <param name="userId">Identifier of the administrator's own account.</param>
public class OwnAccountChangeException(int userId)
    : Exception("You cannot change your own role or remove your own access. Ask another administrator.")
{
    /// <summary>
    /// Identifier of the administrator's own account.
    /// </summary>
    public int UserId { get; } = userId;
}
