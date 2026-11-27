using System;
using System.Net;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the main window: decides which page is shown and handles navigation.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Message shown to an employee who signed in with Microsoft but has no active account in the app.
    /// It is the same for a deactivated account and an account never added, so it does not reveal which accounts exist.
    /// </summary>
    private const string NoAccessMessage = "You do not have access to this app. Contact an administrator.";

    /// <summary>
    /// Creates a new API client each time a page needs one.
    /// </summary>
    private readonly Func<IApiClient> _createApiClient;

    /// <summary>
    /// Signs the user in and out, shared by the whole app.
    /// </summary>
    private readonly IAuthService _authService;

    /// <summary>
    /// Signed-in employee and their role, or null before sign-in or when the API could not be reached.
    /// </summary>
    private CurrentUserDto? _currentUser;

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
    /// <c>true</c> when the signed-in employee is an administrator. Shows the Accounts button; the API checks it again on every request.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAdmin { get; set; }

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
    /// Creates the login page, which checks the employee's access after sign-in.
    /// </summary>
    /// <returns>A new login page.</returns>
    private LoginViewModel CreateLoginPage() => new LoginViewModel(_authService, OnSignedInAsync);

    /// <summary>
    /// Asks the API who the employee is. Shows the home page, or goes back to the login page with a message when the
    /// employee has no active account. If the API cannot be reached, the home page opens and shows that the server is unavailable.
    /// </summary>
    private async Task OnSignedInAsync()
    {
        var me = await _createApiClient().GetCurrentUserAsync();
        if (me.StatusCode == HttpStatusCode.Forbidden)
        {
            await SignOutWithMessageAsync(NoAccessMessage);
            return;
        }

        _currentUser = me.Value;
        IsAdmin = me.Value?.Role == RoleCodes.Admin;
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
                await OnSignedInAsync();
            }
        }
        catch (Exception)
        {
            if (CurrentPage is LoginViewModel loginPage)
            {
                loginPage.ErrorMessage = "Could not restore your session. Check your internet connection and sign in again.";
            }
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
    /// Shows the account page. Only offered to administrators.
    /// </summary>
    [RelayCommand]
    private void ShowAccounts()
    {
        if (IsAdmin)
        {
            CurrentPage = new AccountsViewModel(_createApiClient(), _currentUser?.Email);
        }
    }

    /// <summary>
    /// Signs the user out (removes the saved session) and goes back to the login page.
    /// If the saved session cannot be removed, the login page shows an error message.
    /// </summary>
    [RelayCommand]
    private Task SignOutAsync() => SignOutWithMessageAsync(string.Empty);

    /// <summary>
    /// Signs the user out and shows the login page with a message.
    /// </summary>
    /// <param name="message">Message shown on the login page, or empty.</param>
    private async Task SignOutWithMessageAsync(string message)
    {
        var loginPage = CreateLoginPage();
        loginPage.ErrorMessage = message;
        try
        {
            await _authService.SignOutAsync();
        }
        catch (Exception)
        {
            loginPage.ErrorMessage = "Sign-out failed: your session may still be saved on this computer. Restart the app and sign out again.";
        }

        _currentUser = null;
        IsAdmin = false;
        IsSignedIn = false;
        CurrentPage = loginPage;
    }
}
