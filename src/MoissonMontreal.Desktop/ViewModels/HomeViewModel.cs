using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Welcome";
}