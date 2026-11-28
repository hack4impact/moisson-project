using System.Collections.Generic;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// How an account request to the API ended.
/// </summary>
public enum UserResultStatus
{
    /// <summary>
    /// The API answered with the account or the list of accounts.
    /// </summary>
    Success,

    /// <summary>
    /// The API refused one or more fields (400). The messages are in <see cref="UserResult.Errors"/>.
    /// </summary>
    Invalid,

    /// <summary>
    /// Another account already uses this email (409).
    /// </summary>
    Conflict,

    /// <summary>
    /// The user is not signed in (401).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The role of the user cannot manage accounts (403).
    /// </summary>
    Forbidden,

    /// <summary>
    /// The API could not be reached or gave an unexpected answer.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Answer of the API to an account creation.
/// </summary>
/// <param name="Status">How the request ended.</param>
/// <param name="User">The account created, when <paramref name="Status"/> is <see cref="UserResultStatus.Success"/>.</param>
/// <param name="Errors">One message per refused field, keyed by the field name, when <paramref name="Status"/> is <see cref="UserResultStatus.Invalid"/>.</param>
public record UserResult(UserResultStatus Status, UserDto? User, IReadOnlyDictionary<string, string[]> Errors)
{
    /// <summary>
    /// Creates a result with no account and no field error.
    /// </summary>
    /// <param name="status">How the request ended.</param>
    /// <returns>The result.</returns>
    public static UserResult Of(UserResultStatus status) => new(status, null, new Dictionary<string, string[]>());
}

/// <summary>
/// Answer of the API to the list of accounts.
/// </summary>
/// <param name="Status">How the request ended.</param>
/// <param name="Users">The accounts, empty unless <paramref name="Status"/> is <see cref="UserResultStatus.Success"/>.</param>
public record UserListResult(UserResultStatus Status, IReadOnlyList<UserDto> Users)
{
    /// <summary>
    /// Creates a result with no account.
    /// </summary>
    /// <param name="status">How the request ended.</param>
    /// <returns>The result.</returns>
    public static UserListResult Of(UserResultStatus status) => new(status, []);
}
