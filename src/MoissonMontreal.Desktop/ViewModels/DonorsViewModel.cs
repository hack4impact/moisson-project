using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the donors page (placeholder for now).
/// </summary>
public partial class DonorsViewModel : ViewModelBase
{
    /// <summary>
    /// Title shown at the top of the donors page.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Donors";
}