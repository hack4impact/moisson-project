using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Talks to the central Moisson Montréal API.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Checks whether the API and its database are ready, by calling the API's <c>/health</c> endpoint.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the check early (for example when leaving the page). If cancelled, the method returns <c>false</c>.</param>
    /// <returns><c>true</c> if <c>/health</c> answered 200 (API and database both work); <c>false</c> if the API is unreachable, the database is down (503), or any other error occurs.</returns>
    Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every account, active or not. Only an Admin can do this.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The accounts, or why the API refused or could not answer.</returns>
    Task<UserListResult> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an account with a name, an email and a role. Only an Admin can do this.
    /// </summary>
    /// <param name="request">Fields of the new account.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The account created, or why the API refused it.</returns>
    Task<UserResult> CreateUserAsync(UserRequest request, CancellationToken cancellationToken = default);
}
