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
    /// <exception cref="InvalidOperationException">Thrown when <c>appsettings.json</c> is missing, 
    /// when <c>Api:BaseUrl</c> is missing or invalid, or when a value in the AzureAd section is missing.</exception>
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

            var authSettings = new AuthSettings(
                TenantId: GetRequiredSetting(configuration, "AzureAd:TenantId"),
                ClientId: GetRequiredSetting(configuration, "AzureAd:ClientId"),
                RedirectUri: GetRequiredSetting(configuration, "AzureAd:RedirectUri"),
                Scope: GetRequiredSetting(configuration, "AzureAd:Scope"));
            services.AddSingleton(authSettings);
            services.AddSingleton<IAuthService, AuthService>();

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

    /// <summary>
    /// Reads a required value from the configuration.
    /// </summary>
    /// <param name="configuration">Configuration loaded from <c>appsettings.json</c>.</param>
    /// <param name="key">Key of the value, for example <c>AzureAd:ClientId</c>.</param>
    /// <returns>The value, never empty.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is missing or empty.</exception>
    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Missing '{key}': check that appsettings.json in '{AppContext.BaseDirectory}' contains it.");
        }
        return value;
    }
}