using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    [ObservableProperty]
    public partial string Title { get; set; } = "Welcome";

    [ObservableProperty]
    public partial string ApiStatus { get; set; } = "Checking the API...";

    public HomeViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        CheckApiCommand.Execute(null);
    }

    [RelayCommand]
    private async Task CheckApiAsync()
    {
        ApiStatus = "Checking the API...";
        var isReachable = await _apiClient.IsApiReachableAsync();
        ApiStatus = isReachable ? "API online" : "Server unavailable";
    }
}