namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Entra ID settings the desktop app needs to sign users in with MSAL.
/// These values are public (the app is a public client), so they are read from <c>appsettings.json</c>.
/// </summary>
/// <param name="TenantId">ID of the Moisson Montréal Entra ID tenant.</param>
/// <param name="ClientId">ID of the MoissonMontreal.Desktop app registration.</param>
/// <param name="RedirectUri">Address Microsoft sends the user back to after sign-in (<c>http://localhost</c>).</param>
/// <param name="Scope">Permission requested to call the API (<c>api://…/access_as_user</c>).</param>
public sealed record AuthSettings(string TenantId, string ClientId, string RedirectUri, string Scope);