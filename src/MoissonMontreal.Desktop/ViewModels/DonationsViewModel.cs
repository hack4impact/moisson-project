using CommunityToolkit.Mvvm.ComponentModel;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the donations page (placeholder for now).
/// </summary>
public partial class DonationsViewModel : ViewModelBase
{
    /// <summary>
    /// Title shown at the top of the donations page.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Donations";
}