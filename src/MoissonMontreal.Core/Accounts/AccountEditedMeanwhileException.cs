namespace MoissonMontreal.Core.Accounts;

/// <summary>
/// Thrown when an account was changed by someone else since the administrator loaded it.
/// Nothing is saved, so the other change is not overwritten: the administrator reloads the account and tries again.
/// </summary>
/// <param name="userId">Identifier of the account changed in the meantime.</param>
public class AccountEditedMeanwhileException(int userId)
    : Exception($"Account {userId} was changed by someone else. Reload it and try again.")
{
    /// <summary>
    /// Identifier of the account changed in the meantime.
    /// </summary>
    public int UserId { get; } = userId;
}
