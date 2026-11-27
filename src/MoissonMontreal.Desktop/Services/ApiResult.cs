using System.Net;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Result of a call to the API: the value when it succeeded, or a message the screen can show when it failed.
/// </summary>
/// <typeparam name="T">Type of the value returned by the API.</typeparam>
/// <param name="Value">Value returned by the API, or null when the call failed.</param>
/// <param name="StatusCode">HTTP status of the answer, or null when the API could not be reached.</param>
/// <param name="ErrorMessage">Message explaining the failure, or null when the call succeeded.</param>
public record ApiResult<T>(T? Value, HttpStatusCode? StatusCode, string? ErrorMessage)
{
    /// <summary>
    /// True when the API answered with a value.
    /// </summary>
    public bool IsSuccess => ErrorMessage is null && Value is not null;

    /// <summary>
    /// Builds a successful result.
    /// </summary>
    /// <param name="value">Value returned by the API.</param>
    /// <param name="statusCode">HTTP status of the answer.</param>
    /// <returns>The successful result.</returns>
    public static ApiResult<T> Ok(T value, HttpStatusCode statusCode) => new(value, statusCode, null);

    /// <summary>
    /// Builds a failed result.
    /// </summary>
    /// <param name="statusCode">HTTP status of the answer, or null when the API could not be reached.</param>
    /// <param name="errorMessage">Message explaining the failure.</param>
    /// <returns>The failed result.</returns>
    public static ApiResult<T> Fail(HttpStatusCode? statusCode, string errorMessage) => new(default, statusCode, errorMessage);
}
