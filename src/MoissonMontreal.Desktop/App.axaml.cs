using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoissonMontreal.Desktop.Services;
using MoissonMontreal.Desktop.ViewModels;
using MoissonMontreal.Desktop.Views;


namespace MoissonMontreal.Desktop;

/// <summary>
/// Entry point of the Avalonia application: reads the configuration, sets up the services and opens the main window.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Loads the application's XAML (styles and resources).
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Runs once Avalonia is ready: reads <c>appsettings.json</c>, registers the services and opens the main window.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <c>Api:BaseUrl</c> is missing from <c>appsettings.json</c>.</exception>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            var apiBaseUrl = configuration["Api:BaseUrl"]
                ?? throw new InvalidOperationException("Missing 'Api:BaseUrl' in appsettings.json.");

            var services = new ServiceCollection();

            services.AddHttpClient<IApiClient, ApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            services.AddSingleton<Func<IApiClient>>(sp => () => sp.GetRequiredService<IApiClient>());
            services.AddSingleton<MainViewModel>();

            var serviceProvider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = serviceProvider.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}