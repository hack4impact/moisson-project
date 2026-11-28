using Avalonia.Controls;
using Avalonia.Interactivity;
using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Views;

/// <summary>
/// Temporary account screen for administrators. Its logic is in <see cref="AccountsViewModel"/>.
/// </summary>
public partial class AccountsView : UserControl
{
    /// <summary>
    /// Creates the view from its XAML.
    /// </summary>
    public AccountsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Loads the accounts as soon as the page is shown.
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
}
