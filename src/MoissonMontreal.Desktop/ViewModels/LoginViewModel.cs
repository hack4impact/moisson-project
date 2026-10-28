using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Sign in";
}