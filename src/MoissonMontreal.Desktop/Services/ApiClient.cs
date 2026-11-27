using System.Collections.Generic;
using System.Linq;
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
    /// Message shown when the API cannot be reached at all.
    /// </summary>
    private const string ServerUnavailableMessage = "The server is unavailable. Check your internet connection and try again.";

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
    public Task<ApiResult<CurrentUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        SendAsync<CurrentUserDto>(HttpMethod.Get, "api/me", null, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<List<AccountDto>>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<AccountDto>>(HttpMethod.Get, "api/accounts", null, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<List<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<RoleDto>>(HttpMethod.Get, "api/accounts/roles", null, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> CreateAccountAsync(AccountCreateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AccountDto>(HttpMethod.Post, "api/accounts", request, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> UpdateAccountAsync(int id, AccountUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AccountDto>(HttpMethod.Put, $"api/accounts/{id}", request, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> ChangeAccountRoleAsync(int id, AccountRoleRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AccountDto>(HttpMethod.Put, $"api/accounts/{id}/role", request, cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> DeactivateAccountAsync(int id, uint version, CancellationToken cancellationToken = default) =>
        SendAsync<AccountDto>(HttpMethod.Post, $"api/accounts/{id}/deactivate", new AccountVersionRequest(version), cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> ReactivateAccountAsync(int id, uint version, CancellationToken cancellationToken = default) =>
        SendAsync<AccountDto>(HttpMethod.Post, $"api/accounts/{id}/reactivate", new AccountVersionRequest(version), cancellationToken);

    /// <summary>
    /// Sends a request and turns the answer into a value or a message the screen can show.
    /// </summary>
    /// <typeparam name="T">Type of the value the API returns.</typeparam>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path of the route, relative to the API's base address.</param>
    /// <param name="body">Object sent as JSON, or null for no body.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The value, or a failure with the HTTP status and a message.</returns>
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Fail(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? ApiResult<T>.Fail(response.StatusCode, "The server sent an empty answer.")
                : ApiResult<T>.Ok(value, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(null, ServerUnavailableMessage);
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail(null, ServerUnavailableMessage);
        }
    }

    /// <summary>
    /// Reads the message of an error answer: the field errors of a validation problem, or the detail of another problem.
    /// Falls back to a message based on the status when the answer has no readable body.
    /// </summary>
    /// <param name="response">Error answer of the API.</param>
    /// <param name="cancellationToken">Lets the caller stop reading early.</param>
    /// <returns>A message the screen can show.</returns>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(field => field.Value.EnumerateArray().Select(message => message.GetString()))
                    .Where(message => !string.IsNullOrWhiteSpace(message));
                return string.Join(" ", messages);
            }

            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } detailText)
            {
                return detailText;
            }
        }
        catch (JsonException)
        {
            // The answer has no JSON body, for example a 403 sent by the authorization policy.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Sign out and sign in again.",
            HttpStatusCode.Forbidden => "You do not have access to this action.",
            _ => $"The server refused the request ({(int)response.StatusCode}).",
        };
    }
}
