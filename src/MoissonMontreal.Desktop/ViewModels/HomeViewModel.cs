using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the home page: shows whether the API is reachable.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    /// <summary>
    /// Client used to check whether the API is reachable.
    /// </summary>
    private readonly IApiClient _apiClient;

    /// <summary>
    /// Status message shown while the API check is running.
    /// </summary>
    private const string CheckingApiMessage = "Checking the API...";

    /// <summary>
    /// Status message shown when the API cannot be reached.
    /// </summary>
    private const string ServerUnavailableMessage = "Server unavailable";

    /// <summary>
    /// Status message shown when the API answered successfully.
    /// </summary>
    private const string ApiOnlineMessage = "API online";

    /// <summary>
    /// Title shown at the top of the home page.
    /// </summary>
    public string Title => "Welcome";

    /// <summary>
    /// Message shown when the API refuses to identify the user (no valid token).
    /// </summary>
    private const string NotIdentifiedMessage = "Not identified: the API refused the request";

    /// <summary>
    /// API status message shown to the user.
    /// </summary>
    [ObservableProperty]
    public partial string ApiStatus { get; set; } = CheckingApiMessage;

    /// <summary>
    /// Who the API says is signed in, shown under the API status. Empty while checking or when the API is down.
    /// </summary>
    [ObservableProperty]
    public partial string UserStatus { get; set; } = string.Empty;

    /// <summary>
    /// Creates the home page. The API check starts when the view is loaded.
    /// </summary>
    /// <param name="apiClient">Client used to call the API.</param>
    public HomeViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Checks the API, then asks it who is signed in, and updates <see cref="ApiStatus"/> and <see cref="UserStatus"/>.
    /// Any error shows <see cref="ServerUnavailableMessage"/> instead of crashing the app.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user leaves the home page, so the check doesn't keep running.</param>
    [RelayCommand]
    private async Task CheckApiAsync(CancellationToken cancellationToken)
    {
        ApiStatus = CheckingApiMessage;
        UserStatus = string.Empty;
        try
        {
            var isReachable = await _apiClient.IsApiReachableAsync(cancellationToken);
            ApiStatus = isReachable ? ApiOnlineMessage : ServerUnavailableMessage;

            if (isReachable)
            {
                var user = await _apiClient.GetCurrentUserAsync(cancellationToken);
                UserStatus = user is null
                    ? NotIdentifiedMessage
                    : $"Signed in as {user.Name ?? user.Email ?? "unknown user"}";
            }
        }
        catch (Exception)
        {
            ApiStatus = ServerUnavailableMessage;
        }
    }
}