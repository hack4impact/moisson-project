using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the temporary account screen, for administrators: list the accounts, add an employee, edit an account,
/// change its role and remove or give back its access. Role and access changes ask for a confirmation first.
/// The API enforces every rule; this screen only shows its answers.
/// </summary>
public partial class AccountsViewModel : ViewModelBase
{
    /// <summary>
    /// Client used to call the account routes of the API.
    /// </summary>
    private readonly IApiClient _apiClient;

    /// <summary>
    /// Email of the signed-in administrator, used to disable the actions they cannot do on their own account.
    /// </summary>
    private readonly string? _currentUserEmail;

    /// <summary>
    /// Change waiting for the administrator's confirmation, or null when nothing is waiting.
    /// </summary>
    private Func<Task>? _pendingChange;

    /// <summary>
    /// Title of the page.
    /// </summary>
    public string Title => "Accounts";

    /// <summary>
    /// Every account, active ones first, as returned by the API.
    /// </summary>
    public ObservableCollection<AccountDto> Accounts { get; } = [];

    /// <summary>
    /// Roles an account can be given.
    /// </summary>
    public ObservableCollection<RoleDto> Roles { get; } = [];

    /// <summary>
    /// Account selected in the list, edited in the right-hand panel, or null when none is selected.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(IsOwnAccountSelected), nameof(CanChangeRoleOrAccess))]
    public partial AccountDto? SelectedAccount { get; set; }

    /// <summary>
    /// Email typed in the add form.
    /// </summary>
    [ObservableProperty]
    public partial string NewEmail { get; set; } = string.Empty;

    /// <summary>
    /// Name typed in the add form.
    /// </summary>
    [ObservableProperty]
    public partial string NewDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Role chosen in the add form.
    /// </summary>
    [ObservableProperty]
    public partial RoleDto? NewRole { get; set; }

    /// <summary>
    /// Email of the selected account, as edited.
    /// </summary>
    [ObservableProperty]
    public partial string EditEmail { get; set; } = string.Empty;

    /// <summary>
    /// Name of the selected account, as edited.
    /// </summary>
    [ObservableProperty]
    public partial string EditDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Role chosen for the selected account.
    /// </summary>
    [ObservableProperty]
    public partial RoleDto? EditRole { get; set; }

    /// <summary>
    /// Message confirming the last successful change, or empty.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Message explaining why the last action failed, or empty.
    /// </summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Question shown before a role or access change, or empty when nothing waits for a confirmation.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmationVisible))]
    public partial string ConfirmationMessage { get; set; } = string.Empty;

    /// <summary>
    /// True when an account is selected.
    /// </summary>
    public bool HasSelection => SelectedAccount is not null;

    /// <summary>
    /// True when the selected account is the signed-in administrator's own account.
    /// </summary>
    public bool IsOwnAccountSelected =>
        SelectedAccount is not null &&
        string.Equals(SelectedAccount.Email, _currentUserEmail, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the role and the access of the selected account can be changed from this screen.
    /// </summary>
    public bool CanChangeRoleOrAccess => HasSelection && !IsOwnAccountSelected;

    /// <summary>
    /// True when a confirmation question is shown.
    /// </summary>
    public bool IsConfirmationVisible => ConfirmationMessage.Length > 0;

    /// <summary>
    /// Creates the page. The accounts are loaded when the page is shown.
    /// </summary>
    /// <param name="apiClient">Client used to call the API.</param>
    /// <param name="currentUserEmail">Email of the signed-in administrator.</param>
    public AccountsViewModel(IApiClient apiClient, string? currentUserEmail)
    {
        _apiClient = apiClient;
        _currentUserEmail = currentUserEmail;
    }

    /// <summary>
    /// Loads the roles and the accounts, and selects again the account that was selected.
    /// Clears the messages of the previous action, so they are not mistaken for the result of the reload.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        var selectedId = SelectedAccount?.Id;

        if (Roles.Count == 0)
        {
            var roles = await _apiClient.GetRolesAsync();
            if (!roles.IsSuccess)
            {
                ErrorMessage = roles.ErrorMessage ?? string.Empty;
                return;
            }

            foreach (var role in roles.Value!)
            {
                Roles.Add(role);
            }
        }

        var accounts = await _apiClient.GetAccountsAsync();
        if (!accounts.IsSuccess)
        {
            ErrorMessage = accounts.ErrorMessage ?? string.Empty;
            return;
        }

        Accounts.Clear();
        foreach (var account in accounts.Value!)
        {
            Accounts.Add(account);
        }

        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == selectedId);
    }

    /// <summary>
    /// Adds the employee typed in the add form, then clears the form.
    /// </summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        var result = await _apiClient.CreateAccountAsync(new AccountCreateRequest(NewEmail, NewDisplayName, NewRole?.Code));
        if (await ShowResultAsync(result, $"{NewDisplayName} was added. They can sign in with {NewEmail}."))
        {
            NewEmail = string.Empty;
            NewDisplayName = string.Empty;
            NewRole = null;
        }
    }

    /// <summary>
    /// Saves the name and the email of the selected account.
    /// </summary>
    [RelayCommand]
    private async Task SaveDetailsAsync()
    {
        if (SelectedAccount is not { } account)
        {
            return;
        }

        var result = await _apiClient.UpdateAccountAsync(
            account.Id, new AccountUpdateRequest(EditEmail, EditDisplayName, account.Version));
        await ShowResultAsync(result, "The name and email were saved.");
    }

    /// <summary>
    /// Asks the administrator to confirm the new role of the selected account.
    /// </summary>
    [RelayCommand]
    private void AskChangeRole()
    {
        if (SelectedAccount is not { } account || EditRole is not { } role || role.Code == account.RoleCode)
        {
            return;
        }

        Ask(
            $"Change the role of {account.DisplayName} from {account.RoleName} to {role.Name}? It takes effect on their next action.",
            async () => await ShowResultAsync(
                await _apiClient.ChangeAccountRoleAsync(account.Id, new AccountRoleRequest(role.Code, account.Version)),
                $"{account.DisplayName} is now {role.Name}."));
    }

    /// <summary>
    /// Asks the administrator to confirm removing the access of the selected account.
    /// </summary>
    [RelayCommand]
    private void AskDeactivate()
    {
        if (SelectedAccount is not { IsActive: true } account)
        {
            return;
        }

        Ask(
            $"Remove the access of {account.DisplayName}? They are refused from their next action. The account and its history are kept.",
            async () => await ShowResultAsync(
                await _apiClient.DeactivateAccountAsync(account.Id, account.Version),
                $"{account.DisplayName} no longer has access."));
    }

    /// <summary>
    /// Asks the administrator to confirm giving back the access of the selected account.
    /// </summary>
    [RelayCommand]
    private void AskReactivate()
    {
        if (SelectedAccount is not { IsActive: false } account)
        {
            return;
        }

        Ask(
            $"Give the access back to {account.DisplayName}?",
            async () => await ShowResultAsync(
                await _apiClient.ReactivateAccountAsync(account.Id, account.Version),
                $"{account.DisplayName} has access again."));
    }

    /// <summary>
    /// Runs the change the administrator confirmed.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var change = _pendingChange;
        CancelConfirmation();
        if (change is not null)
        {
            await change();
        }
    }

    /// <summary>
    /// Closes the confirmation question without changing anything.
    /// </summary>
    [RelayCommand]
    private void CancelConfirmation()
    {
        _pendingChange = null;
        ConfirmationMessage = string.Empty;
    }

    /// <summary>
    /// Fills the edit panel with the newly selected account and clears the previous messages.
    /// </summary>
    /// <param name="value">Newly selected account, or null.</param>
    partial void OnSelectedAccountChanged(AccountDto? value)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        EditEmail = value?.Email ?? string.Empty;
        EditDisplayName = value?.DisplayName ?? string.Empty;
        EditRole = Roles.FirstOrDefault(r => r.Code == value?.RoleCode);
        CancelConfirmation();
    }

    /// <summary>
    /// Shows a confirmation question and keeps the change to run if the administrator confirms.
    /// </summary>
    /// <param name="question">Question shown to the administrator.</param>
    /// <param name="change">Change to run after the confirmation.</param>
    private void Ask(string question, Func<Task> change)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        _pendingChange = change;
        ConfirmationMessage = question;
    }

    /// <summary>
    /// Shows the outcome of a change and reloads the list when it succeeded, so the versions shown stay current.
    /// </summary>
    /// <param name="result">Answer of the API.</param>
    /// <param name="successMessage">Message shown when the change succeeded.</param>
    /// <returns>True when the change succeeded.</returns>
    private async Task<bool> ShowResultAsync(ApiResult<AccountDto> result, string successMessage)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage ?? string.Empty;
            return false;
        }

        await LoadAsync();
        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == result.Value!.Id);
        StatusMessage = successMessage;
        return true;
    }
}
