using Avalonia.Controls;
using Avalonia.Interactivity;
using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Views;

/// <summary>
/// Main window of the app. Shows the navigation bar and the current page.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Creates the window and loads its XAML.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Tries to restore the saved session once the window is shown, so a signed-in user skips the login page.
    /// </summary>
    /// <param name="e">Information about the event.</param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RestoreSessionCommand.Execute(null);
        }
    }
}