using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the temporary accounts page: lists every account and creates one with a name, an email and a role.
/// Each field refused by the API shows its message under the field.
/// </summary>
public partial class AccountsViewModel : ViewModelBase
{
    /// <summary>
    /// Client used to read and create accounts.
    /// </summary>
    private readonly IApiClient _apiClient;

    /// <summary>
    /// Creates the accounts page with an empty form. The list is loaded when the view is shown.
    /// </summary>
    /// <param name="apiClient">Client used to call the API.</param>
    public AccountsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Title shown at the top of the accounts page.
    /// </summary>
    public string Title => "Accounts";

    /// <summary>
    /// Role codes the admin can pick for a new account.
    /// </summary>
    public IReadOnlyList<string> Roles => RoleCodes.All;

    /// <summary>
    /// Accounts shown in the list.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<AccountRow> Accounts { get; set; } = [];

    /// <summary>
    /// Message shown instead of the list when it is empty or could not be loaded, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? ListMessage { get; set; }

    /// <summary>
    /// Name of the new account.
    /// </summary>
    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Message under the name field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? DisplayNameError { get; set; }

    /// <summary>
    /// Work email of the new account.
    /// </summary>
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    /// <summary>
    /// Message under the email field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? EmailError { get; set; }

    /// <summary>
    /// Role code of the new account, or null until one is picked.
    /// </summary>
    [ObservableProperty]
    public partial string? Role { get; set; }

    /// <summary>
    /// Message under the role field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? RoleError { get; set; }

    /// <summary>
    /// Result of the last creation, shown to the user.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    /// Loads the list of accounts from the API.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user leaves the page.</param>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ListMessage = "Loading the accounts...";
        var result = await _apiClient.GetUsersAsync(cancellationToken);
        if (result.Status != UserResultStatus.Success)
        {
            Accounts = [];
            ListMessage = Explain(result.Status);
            return;
        }

        var names = result.Users.ToDictionary(user => user.Id, user => user.DisplayName);
        Accounts = result.Users.Select(user => AccountRow.From(user, names)).ToList();
        ListMessage = Accounts.Count == 0 ? "No account yet." : null;
    }

    /// <summary>
    /// Creates the account, then empties the form and reloads the list.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user leaves the page.</param>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        DisplayNameError = null;
        EmailError = null;
        RoleError = null;
        StatusMessage = null;

        var result = await _apiClient.CreateUserAsync(new UserRequest(DisplayName, Email, Role), cancellationToken);
        switch (result.Status)
        {
            case UserResultStatus.Success:
                DisplayName = string.Empty;
                Email = string.Empty;
                Role = null;
                StatusMessage = $"Account created for {result.User!.Email}.";
                await LoadAsync(cancellationToken);
                break;
            case UserResultStatus.Invalid:
                foreach (var (field, messages) in result.Errors)
                {
                    SetError(field, string.Join(" ", messages));
                }

                StatusMessage = "Some fields need to be fixed.";
                break;
            case UserResultStatus.Conflict:
                EmailError = "Another account already uses this email.";
                StatusMessage = "Some fields need to be fixed.";
                break;
            default:
                StatusMessage = Explain(result.Status);
                break;
        }
    }

    /// <summary>
    /// Shows a message under the field the API named.
    /// </summary>
    /// <param name="field">Name of the field, as in <see cref="UserRequest"/>.</param>
    /// <param name="message">Message to show.</param>
    private void SetError(string field, string message)
    {
        switch (field)
        {
            case nameof(UserRequest.DisplayName): DisplayNameError = message; break;
            case nameof(UserRequest.Email): EmailError = message; break;
            case nameof(UserRequest.Role): RoleError = message; break;
        }
    }

    /// <summary>
    /// Message for a request that did not succeed.
    /// </summary>
    /// <param name="status">How the request ended.</param>
    /// <returns>The message to show.</returns>
    private static string Explain(UserResultStatus status) => status switch
    {
        UserResultStatus.Unauthorized => "You must be signed in to manage accounts.",
        UserResultStatus.Forbidden => "Only an Admin can manage accounts.",
        _ => "Server unavailable",
    };
}

/// <summary>
/// One account as shown in the list.
/// </summary>
/// <param name="DisplayName">Name of the employee.</param>
/// <param name="Email">Work email.</param>
/// <param name="Role">Role code.</param>
/// <param name="Access">Active, or Access removed.</param>
/// <param name="CreatedOn">Local date the account was created.</param>
/// <param name="CreatedBy">Name of the admin who created the account, or a note when it was added directly in the database.</param>
public record AccountRow(string DisplayName, string Email, string Role, string Access, string CreatedOn, string CreatedBy)
{
    /// <summary>
    /// Builds the row of an account.
    /// </summary>
    /// <param name="user">Account returned by the API.</param>
    /// <param name="names">Name of every account, by id, to show the creator.</param>
    /// <returns>The row to show.</returns>
    public static AccountRow From(UserDto user, IReadOnlyDictionary<int, string> names) => new(
        user.DisplayName,
        user.Email,
        user.Role,
        user.IsActive ? "Active" : "Access removed",
        user.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd"),
        user.CreatedById is { } id && names.TryGetValue(id, out var name) ? name : "Added in the database");
}
