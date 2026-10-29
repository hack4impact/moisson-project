using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the login page. Sign-in is simulated for now (real sign-in comes with Entra ID).
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    /// <summary>
    /// Title shown at the top of the login page.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Moisson Montréal";

    /// <summary>
    /// Action to run once the user is signed in (opens the home page).
    /// </summary>
    private readonly Action _onSignedIn;

    /// <summary>
    /// Creates the login page.
    /// </summary>
    /// <param name="onSignedIn">Action to run once the user is signed in.</param>
    public LoginViewModel(Action onSignedIn)
    {
        _onSignedIn = onSignedIn;
    }

    /// <summary>
    /// Signs the user in (simulated) and runs the "signed in" action.
    /// </summary>
    [RelayCommand]
    private void SignIn()
    {
        _onSignedIn();
    }
}