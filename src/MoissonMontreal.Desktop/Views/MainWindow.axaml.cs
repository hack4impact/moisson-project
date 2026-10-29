using Avalonia.Controls;

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
}