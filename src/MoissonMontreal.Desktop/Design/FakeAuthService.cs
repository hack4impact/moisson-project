using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.Design;

/// <summary>
/// Fake sign-in service for the IDE previewer. Never contacts Microsoft.
/// </summary>
public class FakeAuthService : IAuthService
{
    /// <inheritdoc/>
    public string? UserName => "preview@example.org";

    /// <inheritdoc/>
    public Task<bool> TrySignInSilentlyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task SignInInteractiveAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("fake-token");
    }

    /// <inheritdoc/>
    public Task SignOutAsync()
    {
        return Task.CompletedTask;
    }
}