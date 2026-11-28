using MoissonMontreal.Desktop.ViewModels;

namespace MoissonMontreal.Desktop.Design;

/// <summary>
/// Ready-made ViewModels for the IDE previewer. Never used when the app runs.
/// </summary>
public static class DesignData
{
    /// <summary>
    /// Main window ViewModel backed by a <see cref="FakeApiClient"/> and a <see cref="FakeAuthService"/>.
    /// </summary>
    public static MainViewModel Main { get; } = new(() => new FakeApiClient(), new FakeAuthService());

    /// <summary>
    /// Home page ViewModel backed by a <see cref="FakeApiClient"/>.
    /// </summary>
    public static HomeViewModel Home { get; } = new(new FakeApiClient());

    /// <summary>
    /// Login page ViewModel whose sign-in does nothing.
    /// </summary>
    public static LoginViewModel Login { get; } = new(new FakeAuthService(), () => System.Threading.Tasks.Task.CompletedTask);

    /// <summary>
    /// Account page ViewModel backed by a <see cref="FakeApiClient"/>, signed in as the preview administrator.
    /// </summary>
    public static AccountsViewModel Accounts { get; } = new(new FakeApiClient(), "preview@example.org");

    /// <summary>
    /// Donors page ViewModel backed by a <see cref="FakeApiClient"/>.
    /// </summary>
    public static DonorsViewModel Donors { get; } = new(new FakeApiClient());
}
