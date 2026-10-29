using Avalonia.Controls;
using Avalonia.Interactivity;

using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Views;

/// <summary>
/// Home page screen.
/// </summary>
public partial class HomeView : UserControl
{
    /// <summary>
    /// Creates the view and loads its XAML.
    /// </summary>
    public HomeView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Starts the API check once the view is shown on screen.
    /// </summary>
    /// <param name="e">Information about the event.</param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is HomeViewModel viewModel)
        {
            viewModel.CheckApiCommand.Execute(null);
        }
    }
}