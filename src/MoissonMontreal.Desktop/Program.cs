using Avalonia;
using System;

namespace MoissonMontreal.Desktop;

/// <summary>
/// Starts the desktop application.
/// </summary>
sealed class Program
{
    /// <summary>
    /// Program entry point: starts Avalonia with a classic desktop window.
    /// </summary>
    /// <remarks>
    /// Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant code
    /// before AppMain is called: things aren't initialized yet and stuff might break.
    /// </remarks>
    /// <param name="args">Command-line arguments.</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Configures Avalonia. Don't remove: the visual designer uses it too.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
