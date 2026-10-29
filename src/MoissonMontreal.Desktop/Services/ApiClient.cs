using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

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
            // Any endpoint that answers successfully proves the API is reachable.
            using var response = await _httpClient.GetAsync("WeatherForecast", cancellationToken);
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
}