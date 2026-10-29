using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class DonationsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Donations";
}