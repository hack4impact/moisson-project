using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Desktop.Services;
using MoissonMontreal.Core.Dtos;

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
    public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = new CurrentUserDto(
            ObjectId: "00000000-0000-0000-0000-000000000000",
            Name: "Preview User",
            Email: "preview@example.org",
            TenantId: null);
        return Task.FromResult<CurrentUserDto?>(user);
    }
}
