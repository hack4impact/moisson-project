using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class DonorsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Donors";
}