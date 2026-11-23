using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    public Task<DonorResult> GetDonorAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendDonorAsync(() => _httpClient.GetAsync($"api/donors/{id}", cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task<DonorResult> CreateDonorAsync(DonorRequest request, CancellationToken cancellationToken = default) =>
        SendDonorAsync(() => _httpClient.PostAsJsonAsync("api/donors", request, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task<DonorResult> UpdateDonorAsync(Guid id, DonorRequest request, CancellationToken cancellationToken = default) =>
        SendDonorAsync(() => _httpClient.PutAsJsonAsync($"api/donors/{id}", request, cancellationToken), cancellationToken);

    /// <summary>
    /// Sends a donor request and turns the answer into a <see cref="DonorResult"/>. Network errors and unexpected answers give <see cref="DonorResultStatus.Unavailable"/>.
    /// </summary>
    /// <param name="send">Sends the request.</param>
    /// <param name="cancellationToken">Lets the caller stop reading the answer early.</param>
    /// <returns>How the request ended.</returns>
    private static async Task<DonorResult> SendDonorAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK or HttpStatusCode.Created:
                    var donor = await response.Content.ReadFromJsonAsync<DonorDto>(cancellationToken);
                    return donor is null
                        ? DonorResult.Of(DonorResultStatus.Unavailable)
                        : new DonorResult(DonorResultStatus.Success, donor, new Dictionary<string, string[]>());
                case HttpStatusCode.BadRequest:
                    var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(cancellationToken);
                    return new DonorResult(DonorResultStatus.Invalid, null, problem?.Errors ?? new Dictionary<string, string[]>());
                case HttpStatusCode.NotFound:
                    return DonorResult.Of(DonorResultStatus.NotFound);
                case HttpStatusCode.Conflict:
                    return DonorResult.Of(DonorResultStatus.Conflict);
                case HttpStatusCode.Unauthorized:
                    return DonorResult.Of(DonorResultStatus.Unauthorized);
                case HttpStatusCode.Forbidden:
                    return DonorResult.Of(DonorResultStatus.Forbidden);
                default:
                    return DonorResult.Of(DonorResultStatus.Unavailable);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return DonorResult.Of(DonorResultStatus.Unavailable);
        }
    }

    /// <summary>
    /// Field errors of a 400 answer from the API.
    /// </summary>
    /// <param name="Errors">One list of messages per refused field.</param>
    private sealed record ValidationProblem(Dictionary<string, string[]>? Errors);
}
