using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Identity.Client;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the login page: signs the user in with their Microsoft work account (Entra ID).
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    /// <summary>
    /// Title shown at the top of the login page.
    /// </summary>
    public string Title => "Moisson Montréal";

    /// <summary>
    /// Service that opens the Microsoft sign-in page.
    /// </summary>
    private readonly IAuthService _authService;

    /// <summary>
    /// Action to run once the user is signed in (opens the home page).
    /// </summary>
    private readonly Action _onSignedIn;

    /// <summary>
    /// Message shown when sign-in fails or is cancelled. Empty when there is no error.
    /// </summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Creates the login page.
    /// </summary>
    /// <param name="authService">Service that signs the user in.</param>
    /// <param name="onSignedIn">Action to run once the user is signed in.</param>
    public LoginViewModel(IAuthService authService, Action onSignedIn)
    {
        _authService = authService;
        _onSignedIn = onSignedIn;
    }

    /// <summary>
    /// Opens the Microsoft sign-in page in the browser, then runs the "signed in" action if it succeeds.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user clicks Cancel while the browser is open.</param>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        try
        {
            await _authService.SignInInteractiveAsync(cancellationToken);
            _onSignedIn();
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Sign-in cancelled.";
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            ErrorMessage = "Sign-in cancelled.";
        }
        catch (MsalException)
        {
            ErrorMessage = "Sign-in failed. Check your internet connection and try again.";
        }
    }
}