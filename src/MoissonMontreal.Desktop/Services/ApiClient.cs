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
    public async Task<UserListResult> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/users", cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return UserListResult.Of(FailureStatus(response.StatusCode));
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>(cancellationToken);
            return users is null
                ? UserListResult.Of(UserResultStatus.Unavailable)
                : new UserListResult(UserResultStatus.Success, users);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return UserListResult.Of(UserResultStatus.Unavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<UserResult> CreateUserAsync(UserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/users", request, cancellationToken);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                    var user = await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken);
                    return user is null
                        ? UserResult.Of(UserResultStatus.Unavailable)
                        : new UserResult(UserResultStatus.Success, user, new Dictionary<string, string[]>());
                case HttpStatusCode.BadRequest:
                    var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(cancellationToken);
                    return new UserResult(UserResultStatus.Invalid, null, problem?.Errors ?? new Dictionary<string, string[]>());
                default:
                    return UserResult.Of(FailureStatus(response.StatusCode));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return UserResult.Of(UserResultStatus.Unavailable);
        }
    }

    /// <summary>
    /// Turns an answer that is not a success or a field error into a status.
    /// </summary>
    /// <param name="statusCode">HTTP status of the answer.</param>
    /// <returns>Conflict for 409, Unauthorized for 401, Forbidden for 403, otherwise Unavailable.</returns>
    private static UserResultStatus FailureStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Conflict => UserResultStatus.Conflict,
        HttpStatusCode.Unauthorized => UserResultStatus.Unauthorized,
        HttpStatusCode.Forbidden => UserResultStatus.Forbidden,
        _ => UserResultStatus.Unavailable,
    };

    /// <summary>
    /// Field errors of a 400 answer from the API.
    /// </summary>
    /// <param name="Errors">One list of messages per refused field.</param>
    private sealed record ValidationProblem(Dictionary<string, string[]>? Errors);
}
