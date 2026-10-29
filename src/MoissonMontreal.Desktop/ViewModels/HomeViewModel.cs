using System;
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
    [ObservableProperty]
    public partial string Title { get; set; } = "Welcome";

    /// <summary>
    /// API status message shown to the user.
    /// </summary>
    [ObservableProperty]
    public partial string ApiStatus { get; set; } = CheckingApiMessage;

    /// <summary>
    /// Creates the home page. The API check starts when the view is loaded.
    /// </summary>
    /// <param name="apiClient">Client used to call the API.</param>
    public HomeViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Checks the API and updates <see cref="ApiStatus"/>. Any error shows <see cref="ServerUnavailableMessage"/> instead of crashing the app.
    /// </summary>
    [RelayCommand]
    private async Task CheckApiAsync()
    {
        ApiStatus = CheckingApiMessage;
        try
        {
            var isReachable = await _apiClient.IsApiReachableAsync();
            ApiStatus = isReachable ? ApiOnlineMessage : ServerUnavailableMessage;
        }
        catch (Exception)
        {
            ApiStatus = ServerUnavailableMessage;
        }
    }
}