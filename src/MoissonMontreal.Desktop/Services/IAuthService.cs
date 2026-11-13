using System.Threading;
using System.Threading.Tasks;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Signs the user in and out with their Moisson Montréal Microsoft work account (Entra ID),
/// and provides the access token sent to the API.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Sign-in name (usually the work email) of the signed-in user, or <c>null</c> when nobody is signed in.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Tries to sign the user in without showing any window, using an account already in the token cache.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the attempt early.</param>
    /// <returns><c>true</c> if the user is signed in; <c>false</c> if an interactive sign-in is needed.</returns>
    Task<bool> TrySignInSilentlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the Microsoft sign-in page in the system browser and waits for the user to sign in.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop waiting for the user.</param>
    /// <exception cref="Microsoft.Identity.Client.MsalException">Thrown when sign-in fails or the user cancels it.</exception>
    Task SignInInteractiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a valid access token for the API, renewing it silently if it has expired.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The access token to put in the <c>Authorization</c> header.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when nobody is signed in, or the session has expired 
    /// and the user must sign in again.</exception>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs the user out by removing their accounts and tokens from the cache.
    /// </summary>
    Task SignOutAsync();
}