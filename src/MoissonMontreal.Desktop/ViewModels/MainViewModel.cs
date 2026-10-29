using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly Func<IApiClient> _createApiClient;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    public partial ViewModelBase CurrentPage  {get; set;}

    public bool IsSignedIn => CurrentPage is not LoginViewModel;

    public MainViewModel(Func<IApiClient> createApiClient)
    {
        _createApiClient = createApiClient;
        CurrentPage = CreateLoginPage();
    }

    private LoginViewModel CreateLoginPage() => new LoginViewModel(ShowHome);

    [RelayCommand]
    private void ShowHome()
    {
        CurrentPage = new HomeViewModel(_createApiClient());
    }

    [RelayCommand]
    private void ShowDonations()
    {
        CurrentPage = new DonationsViewModel();
    }

    [RelayCommand]
    private void ShowDonors()
    {
        CurrentPage = new DonorsViewModel();
    }

    [RelayCommand]
    private void SignOut()
    {
        CurrentPage = CreateLoginPage();
    }
}
