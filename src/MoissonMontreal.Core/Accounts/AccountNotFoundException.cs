namespace MoissonMontreal.Core.Accounts;

/// <summary>
/// Thrown when an account is changed but no account has this identifier.
/// </summary>
/// <param name="userId">Identifier that matches no account.</param>
public class AccountNotFoundException(int userId) : Exception($"Account {userId} does not exist.")
{
    /// <summary>
    /// Identifier that matches no account.
    /// </summary>
    public int UserId { get; } = userId;
}
