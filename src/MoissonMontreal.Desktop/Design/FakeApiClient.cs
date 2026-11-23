using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.Design;

/// <summary>
/// Fake API client for the IDE previewer. Always reports the API as online, without any network call.
/// </summary>
public class FakeApiClient : IApiClient
{
    /// <inheritdoc/>
    public Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<DonorResult> GetDonorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DonorResult.Of(DonorResultStatus.NotFound));
    }

    /// <inheritdoc/>
    public Task<DonorResult> CreateDonorAsync(DonorRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Saved(Guid.NewGuid(), request, null));
    }

    /// <inheritdoc/>
    public Task<DonorResult> UpdateDonorAsync(Guid id, DonorRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Saved(id, request, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Returns the request as if the API had saved it.
    /// </summary>
    /// <param name="id">Identifier given to the donor.</param>
    /// <param name="request">Fields of the donor.</param>
    /// <param name="updatedAt">Date and time of the change, or null for a new donor.</param>
    /// <returns>A successful result.</returns>
    private static DonorResult Saved(Guid id, DonorRequest request, DateTimeOffset? updatedAt)
    {
        var donor = new DonorDto(
            id,
            request.Name ?? string.Empty,
            request.Type ?? string.Empty,
            request.Address,
            request.City,
            request.Province,
            request.PostalCode,
            request.ContactPerson,
            request.Email,
            request.Phone,
            request.Notes,
            request.BusinessCentralCode,
            DateTimeOffset.UtcNow,
            updatedAt);
        return new DonorResult(DonorResultStatus.Success, donor, new Dictionary<string, string[]>());
    }
}
