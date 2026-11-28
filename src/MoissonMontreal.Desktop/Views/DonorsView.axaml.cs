using Avalonia.Controls;
using Avalonia.Interactivity;
using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Views;

/// <summary>
/// Donors page screen.
/// </summary>
public partial class DonorsView : UserControl
{
    /// <summary>
    /// Creates the view and loads its XAML.
    /// </summary>
    public DonorsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Stops the request in progress when the user leaves the page.
    /// </summary>
    /// <param name="e">Data of the event.</param>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (DataContext is DonorsViewModel viewModel)
        {
            viewModel.OpenCommand.Cancel();
            viewModel.SaveCommand.Cancel();
        }
    }
}
