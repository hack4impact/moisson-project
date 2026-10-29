using System.Threading.Tasks;
using System.Net.Http;

namespace MoissonMontreal.Desktop.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsApiReachableAsync()
    {
        try
        {
            // Any endpoint that answers successfully proves the API is reachable.
            using var response = await _httpClient.GetAsync("WeatherForecast");
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