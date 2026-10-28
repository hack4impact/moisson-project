using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Moisson Montréal";

    private readonly Action _onSignedIn;

    public LoginViewModel(Action onSignedIn)
    {
        _onSignedIn = onSignedIn;
    }
    
    [RelayCommand]
    private void SignIn()
    {
        _onSignedIn();
    }
}