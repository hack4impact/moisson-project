using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Design;

/// <summary>
/// Ready-made ViewModels for the IDE previewer. Never used when the app runs.
/// </summary>
public static class DesignData
{
    /// <summary>
    /// Main window ViewModel backed by a <see cref="FakeApiClient"/>.
    /// </summary>
    public static MainViewModel Main { get; } = new(() => new FakeApiClient());

    /// <summary>
    /// Home page ViewModel backed by a <see cref="FakeApiClient"/>.
    /// </summary>
    public static HomeViewModel Home { get; } = new(new FakeApiClient());

    /// <summary>
    /// Login page ViewModel whose sign-in does nothing.
    /// </summary>
    public static LoginViewModel Login { get; } = new(() => { });
}
