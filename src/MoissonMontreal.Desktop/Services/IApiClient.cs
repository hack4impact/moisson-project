using System.Threading;
using System.Threading.Tasks;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Talks to the central Moisson Montréal API.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Checks whether the API answers.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the check early (for example when leaving the page). If cancelled, the method returns <c>false</c>.</param>
    /// <returns><c>true</c> if the API answered successfully; <c>false</c> if it is unreachable or returned an error.</returns>
    Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default);
}


