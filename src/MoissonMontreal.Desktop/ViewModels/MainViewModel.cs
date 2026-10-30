using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    /// Page currently shown in the main window.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    public partial ViewModelBase CurrentPage  {get; set;}

    /// <summary>
    /// <c>true</c> when the user is past the login page; shows the navigation bar and the Sign out button.
    /// </summary>
    public bool IsSignedIn => CurrentPage is not LoginViewModel;

    /// <summary>
    /// Creates the main ViewModel and shows the login page.
    /// </summary>
    /// <param name="createApiClient">Function that returns a new API client on each call.</param>
    public MainViewModel(Func<IApiClient> createApiClient)
    {
        _createApiClient = createApiClient;
        CurrentPage = CreateLoginPage();
    }

    /// <summary>
    /// Creates the login page, which opens the home page after sign-in.
    /// </summary>
    /// <returns>A new login page.</returns>
    private LoginViewModel CreateLoginPage() => new LoginViewModel(ShowHome);

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
    /// Signs the user out and goes back to the login page.
    /// </summary>
    [RelayCommand]
    private void SignOut()
    {
        CurrentPage = CreateLoginPage();
    }
}
