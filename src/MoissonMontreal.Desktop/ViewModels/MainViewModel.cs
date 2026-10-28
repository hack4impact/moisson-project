using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ViewModelBase CurrentPage  { get; set; }

    public MainViewModel()
    {
        CurrentPage = new LoginViewModel(() => CurrentPage = new HomeViewModel());
    }
}
