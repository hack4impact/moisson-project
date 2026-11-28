using Avalonia.Controls;
using Avalonia.Interactivity;
using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Views;

/// <summary>
/// Accounts page screen.
/// </summary>
public partial class AccountsView : UserControl
{
    /// <summary>
    /// Creates the view and loads its XAML.
    /// </summary>
    public AccountsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Loads the list of accounts once the view is shown on screen.
    /// </summary>
    /// <param name="e">Information about the event.</param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is AccountsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }

    /// <summary>
    /// Stops loading the list and creating an account when the view is removed from the screen.
    /// </summary>
    /// <param name="e">Information about the event.</param>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (DataContext is AccountsViewModel viewModel)
        {
            viewModel.LoadCommand.Cancel();
            viewModel.CreateCommand.Cancel();
        }
    }
}
