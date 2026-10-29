using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    public partial ViewModelBase CurrentPage  {get; set;}

    public bool IsSignedIn => CurrentPage is not LoginViewModel;

    public MainViewModel()
    {
        CurrentPage = CreateLoginPage();
    }

    private LoginViewModel CreateLoginPage() => new LoginViewModel(ShowHome);

    [RelayCommand]
    private void ShowHome()
    {
        CurrentPage = new HomeViewModel();
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
