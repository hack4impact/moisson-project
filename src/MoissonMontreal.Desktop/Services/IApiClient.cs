using System.Threading;
using System.Threading.Tasks;

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
}


