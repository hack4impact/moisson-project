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
    /// <exception cref="InvalidOperationException">Thrown when <c>appsettings.json</c> is missing, or when <c>Api:BaseUrl</c> is missing or invalid.</exception>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var apiBaseUrl = configuration["Api:BaseUrl"]
                ?? throw new InvalidOperationException(
                    $"Missing 'Api:BaseUrl': check that appsettings.json exists in '{AppContext.BaseDirectory}' and contains it.");

            // HttpClient drops the last path segment of a base address without a trailing "/".
            if (!apiBaseUrl.EndsWith("/"))
            {
                apiBaseUrl += "/";
            }

            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri)
                || (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"Invalid 'Api:BaseUrl' in appsettings.json: '{apiBaseUrl}'. It must be a full URL starting with http:// or https://.");
            }

            var services = new ServiceCollection();

            services.AddHttpClient<IApiClient, ApiClient>(client =>
            {
                client.BaseAddress = apiBaseUri;
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