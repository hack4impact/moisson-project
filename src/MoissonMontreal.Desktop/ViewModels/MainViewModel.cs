using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Identity.Client;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the main window: decides which page is shown and handles navigation.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a new API client each time the home page is opened.
    /// </summary>
    private readonly Func<IApiClient> _createApiClient;

    /// <summary>
    /// Signs the user in and out, shared by the whole app.
    /// </summary>
    private readonly IAuthService _authService;

    /// <summary>
    /// Page currently shown in the main window.
    /// </summary>
    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    /// <summary>
    /// <c>true</c> once the user has signed in, <c>false</c> after sign-out. Shows the navigation bar and the Sign out button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    /// <summary>
    /// Creates the main ViewModel and shows the login page.
    /// </summary>
    /// <param name="createApiClient">Function that returns a new API client on each call.</param>
    /// <param name="authService">Service that signs the user in and out.</param>
    public MainViewModel(Func<IApiClient> createApiClient, IAuthService authService)
    {
        _createApiClient = createApiClient;
        _authService = authService;
        CurrentPage = CreateLoginPage();
    }

    /// <summary>
    /// Creates the login page, which opens the home page after sign-in.
    /// </summary>
    /// <returns>A new login page.</returns>
    private LoginViewModel CreateLoginPage() => new LoginViewModel(_authService, OnSignedIn);

    /// <summary>
    /// Marks the user as signed in and shows the home page.
    /// </summary>
    private void OnSignedIn()
    {
        IsSignedIn = true;
        ShowHome();
    }

    /// <summary>
    /// Signs the user back in without any window if a session was saved, and shows the home page.
    /// Stays on the login page if there is no saved session or Microsoft cannot be reached.
    /// </summary>
    [RelayCommand]
    private async Task RestoreSessionAsync()
    {
        try
        {
            if (await _authService.TrySignInSilentlyAsync())
            {
                OnSignedIn();
            }
        }
        catch (MsalException)
        {
            // For example no internet at startup: the user can still sign in with the button.
        }
    }

    /// <summary>
    /// Shows the home page with a new API client.
    /// </summary>
    [RelayCommand]
    private void ShowHome()
    {
        CurrentPage = new HomeViewModel(_createApiClient());
    }

    /// <summary>
    /// Shows the donations page.
    /// </summary>
    [RelayCommand]
    private void ShowDonations()
    {
        CurrentPage = new DonationsViewModel();
    }

    /// <summary>
    /// Shows the donors page.
    /// </summary>
    [RelayCommand]
    private void ShowDonors()
    {
        CurrentPage = new DonorsViewModel();
    }

    /// <summary>
    /// Signs the user out (removes the saved session) and goes back to the login page.
    /// </summary>
    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _authService.SignOutAsync();
        IsSignedIn = false;
        CurrentPage = CreateLoginPage();
    }
}
