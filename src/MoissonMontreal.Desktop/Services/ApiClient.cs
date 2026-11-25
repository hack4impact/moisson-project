using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Json;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Calls the central API over HTTP.
/// </summary>
public class ApiClient : IApiClient
{
    /// <summary>
    /// HTTP client configured in <c>App</c> with the API's base address and timeout.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="httpClient">HTTP client provided by the dependency injection container.</param>
    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // The health endpoint returns 200 only when the API and its database both work (503 if the database is down),
            // so "reachable" means the whole server is ready.
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/me", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CurrentUserDto>(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}