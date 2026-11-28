namespace MoissonMontreal.Core.Accounts;

/// <summary>
/// Thrown when a change would leave the app without any active administrator, so nobody could manage accounts anymore.
/// </summary>
/// <param name="userId">Identifier of the last active administrator.</param>
public class LastActiveAdminException(int userId)
    : Exception($"Account {userId} is the last active administrator: it cannot lose the role or be deactivated.")
{
    /// <summary>
    /// Identifier of the last active administrator.
    /// </summary>
    public int UserId { get; } = userId;
}
