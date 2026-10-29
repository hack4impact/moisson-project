using Avalonia.Controls;
using Avalonia.Interactivity;

using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is HomeViewModel viewModel)
        {
            viewModel.CheckApiCommand.Execute(null);
        }
    }
}