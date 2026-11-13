using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;


namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Signs the user in with Entra ID using MSAL.NET.
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// MSAL client for the desktop app. It holds the token cache, so only one is created for the whole app.
    /// </summary>
    private readonly IPublicClientApplication _msalClient;

    /// <summary>
    /// Permissions requested in each token (here, only access to the API).
    /// </summary>
    private readonly string[] _scopes;

    /// <summary>
    /// Name of the token cache file.
    /// </summary>
    private const string CacheFileName = "msal_cache.bin";

    /// <summary>
    /// Name of the Keychain entry that holds the cache on macOS.
    /// </summary>
    private const string MacKeyChainServiceName = "MoissonMontreal.Desktop";

    /// <summary>
    /// Account name of the Keychain entry on macOS.
    /// </summary>
    private const string MacKeyChainAccountName = "MsalCache";

    /// <summary>
    /// Connects the MSAL cache to disk storage, once, the first time it is needed.
    /// </summary>
    private readonly Lazy<Task> _persistentCache;

    /// <summary>
    /// Creates the service and the MSAL client.
    /// </summary>
    /// <param name="settings">Entra ID settings read from <c>appsettings.json</c>.</param>
    public AuthService(AuthSettings settings)
    {
        _scopes = [settings.Scope];
        _msalClient = PublicClientApplicationBuilder
            .Create(settings.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, settings.TenantId)
            .WithRedirectUri(settings.RedirectUri)
            .Build();
        _persistentCache = new Lazy<Task>(RegisterPersistentCacheAsync);
    }

    /// <inheritdoc/>
    public string? UserName { get; private set; }

    /// <inheritdoc/>
    public async Task<bool> TrySignInSilentlyAsync(CancellationToken cancellationToken = default)
    {
        var result = await AcquireTokenSilentAsync(cancellationToken);
        return result is not null;
    }

    /// <inheritdoc/>
    public async Task SignInInteractiveAsync(CancellationToken cancellationToken = default)
    {
        await _persistentCache.Value;

        var result = await _msalClient
            .AcquireTokenInteractive(_scopes)
            .WithUseEmbeddedWebView(false)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);

        UserName = result.Account.Username;
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await AcquireTokenSilentAsync(cancellationToken)
            ?? throw new InvalidOperationException("No signed-in user, or the session has expired: sign in again.");

        return result.AccessToken;
    }

    /// <inheritdoc/>
    public async Task SignOutAsync()
    {
        await _persistentCache.Value;

        var accounts = await _msalClient.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _msalClient.RemoveAsync(account);
        }

        UserName = null;
    }

    /// <summary>
    /// Gets a token from the cache for the first cached account, renewing it silently if needed.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The MSAL result with the token, or <c>null</c> if there is no cached account or the user must sign in again.</returns>
    private async Task<AuthenticationResult?> AcquireTokenSilentAsync(CancellationToken cancellationToken)
    {
        await _persistentCache.Value;

        var accounts = await _msalClient.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        if (account is null)
        {
            return null;
        }

        try
        {
            var result = await _msalClient
                .AcquireTokenSilent(_scopes, account)
                .ExecuteAsync(cancellationToken);

            UserName = result.Account.Username;
            return result;
        }
        catch (MsalUiRequiredException)
        {
            // The refresh token expired or was revoked (for example after a password change): an interactive sign-in is needed.
            return null;
        }
    }

    /// <summary>
    /// Saves the MSAL token cache to disk, encrypted (DPAPI on Windows, Keychain on macOS),
    /// so the session survives app restarts.
    /// </summary>
    private async Task RegisterPersistentCacheAsync()
    {
        var cacheDirectory = Path.Combine(MsalCacheHelper.UserRootDirectory, "MoissonMontreal");

        var storageProperties = new StorageCreationPropertiesBuilder(CacheFileName, cacheDirectory)
            .WithMacKeyChain(MacKeyChainServiceName, MacKeyChainAccountName)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(_msalClient.UserTokenCache);
    }

}