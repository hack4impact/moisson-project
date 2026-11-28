using System.Collections.Generic;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// How a donor request to the API ended.
/// </summary>
public enum DonorResultStatus
{
    /// <summary>
    /// The API returned the donor.
    /// </summary>
    Success,

    /// <summary>
    /// The API refused one or more fields (400). The messages are in <see cref="DonorResult.Errors"/>.
    /// </summary>
    Invalid,

    /// <summary>
    /// No donor has this id (404).
    /// </summary>
    NotFound,

    /// <summary>
    /// Another donor already uses this Business Central code (409).
    /// </summary>
    Conflict,

    /// <summary>
    /// The user is not signed in (401).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The role of the user cannot do this (403).
    /// </summary>
    Forbidden,

    /// <summary>
    /// The API could not be reached or gave an unexpected answer.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Answer of the API to a donor request.
/// </summary>
/// <param name="Status">How the request ended.</param>
/// <param name="Donor">The donor returned by the API, when <paramref name="Status"/> is <see cref="DonorResultStatus.Success"/>.</param>
/// <param name="Errors">One message per refused field, keyed by the field name, when <paramref name="Status"/> is <see cref="DonorResultStatus.Invalid"/>.</param>
public record DonorResult(DonorResultStatus Status, DonorDto? Donor, IReadOnlyDictionary<string, string[]> Errors)
{
    /// <summary>
    /// Creates a result with no donor and no field error.
    /// </summary>
    /// <param name="status">How the request ended.</param>
    /// <returns>The result.</returns>
    public static DonorResult Of(DonorResultStatus status) => new(status, null, new Dictionary<string, string[]>());
}
