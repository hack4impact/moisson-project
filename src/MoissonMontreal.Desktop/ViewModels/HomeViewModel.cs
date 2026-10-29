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
    /// Title shown at the top of the home page.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Welcome";

    /// <summary>
    /// API status message shown to the user.
    /// </summary>
    [ObservableProperty]
    public partial string ApiStatus { get; set; } = "Checking the API...";

    /// <summary>
    /// Creates the home page. The API check starts when the view is loaded.
    /// </summary>
    /// <param name="apiClient">Client used to call the API.</param>
    public HomeViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Checks the API and updates <see cref="ApiStatus"/>. Any error shows "Server unavailable" instead of crashing the app.
    /// </summary>
    [RelayCommand]
    private async Task CheckApiAsync()
    {
        ApiStatus = "Checking the API...";
        try
        {
            var isReachable = await _apiClient.IsApiReachableAsync();
            ApiStatus = isReachable ? "API online" : "Server unavailable";
        }
        catch (Exception)
        {
            ApiStatus = "Server unavailable";
        }
    }
}