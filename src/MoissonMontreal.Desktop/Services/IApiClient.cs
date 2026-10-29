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
    /// <returns><c>true</c> if the API answered successfully; <c>false</c> if it is unreachable or returned an error.</returns>
    Task<bool> IsApiReachableAsync();
}


