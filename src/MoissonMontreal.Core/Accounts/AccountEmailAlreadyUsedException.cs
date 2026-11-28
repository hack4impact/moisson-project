namespace MoissonMontreal.Core.Accounts;

/// <summary>
/// Thrown when an account is added or edited with an email another account already uses, regardless of case.
/// The other account may be deactivated: it can then be reactivated instead of added again.
/// </summary>
/// <param name="email">Email already used, in lowercase.</param>
public class AccountEmailAlreadyUsedException(string email) : Exception($"The email {email} is already used by another account.")
{
    /// <summary>
    /// Email already used, in lowercase.
    /// </summary>
    public string Email { get; } = email;
}
