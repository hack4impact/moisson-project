using System.Threading;
using System.Threading.Tasks;
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
}
