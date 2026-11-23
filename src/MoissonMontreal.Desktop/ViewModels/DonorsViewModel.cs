using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.ViewModels;

/// <summary>
/// Logic of the temporary donor profile page: opens a donor by its id, creates a donor and edits it.
/// Each field refused by the API shows its message under the field.
/// </summary>
public partial class DonorsViewModel : ViewModelBase
{
    /// <summary>
    /// Client used to read and save donors.
    /// </summary>
    private readonly IApiClient _apiClient;

    /// <summary>
    /// Creates the donor page with an empty form for a new donor.
    /// </summary>
    /// <param name="apiClient">Client used to call the API.</param>
    public DonorsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Title shown at the top of the donors page.
    /// </summary>
    public string Title => "Donors";

    /// <summary>
    /// Types the user can pick for a donor.
    /// </summary>
    public IReadOnlyList<string> Types => DonorTypes.All;

    /// <summary>
    /// Identifier of the donor shown in the form, or null when the form is for a new donor.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    public partial Guid? CurrentDonorId { get; set; }

    /// <summary>
    /// Heading of the form: new donor, or the id of the donor being edited.
    /// </summary>
    public string FormTitle => CurrentDonorId is { } id ? $"Donor {id}" : "New donor";

    /// <summary>
    /// Id typed by the user to open a donor.
    /// </summary>
    [ObservableProperty]
    public partial string DonorIdText { get; set; } = string.Empty;

    /// <summary>
    /// Message under the id field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? DonorIdError { get; set; }

    /// <summary>
    /// Result of the last action, shown to the user.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    /// Name of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// Message under the name field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? NameError { get; set; }

    /// <summary>
    /// Type of the donor, one of <see cref="Types"/>.
    /// </summary>
    [ObservableProperty]
    public partial string? Type { get; set; }

    /// <summary>
    /// Message under the type field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? TypeError { get; set; }

    /// <summary>
    /// Street address of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    /// <summary>
    /// Message under the address field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? AddressError { get; set; }

    /// <summary>
    /// City of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string City { get; set; } = string.Empty;

    /// <summary>
    /// Message under the city field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? CityError { get; set; }

    /// <summary>
    /// Province of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string Province { get; set; } = string.Empty;

    /// <summary>
    /// Message under the province field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? ProvinceError { get; set; }

    /// <summary>
    /// Postal code of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// Message under the postal code field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? PostalCodeError { get; set; }

    /// <summary>
    /// Person to contact at the donor.
    /// </summary>
    [ObservableProperty]
    public partial string ContactPerson { get; set; } = string.Empty;

    /// <summary>
    /// Message under the contact person field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? ContactPersonError { get; set; }

    /// <summary>
    /// Email of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    /// <summary>
    /// Message under the email field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? EmailError { get; set; }

    /// <summary>
    /// Phone number of the donor.
    /// </summary>
    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Message under the phone field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? PhoneError { get; set; }

    /// <summary>
    /// Free notes about the donor.
    /// </summary>
    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Message under the notes field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? NotesError { get; set; }

    /// <summary>
    /// Code of the donor in Business Central.
    /// </summary>
    [ObservableProperty]
    public partial string BusinessCentralCode { get; set; } = string.Empty;

    /// <summary>
    /// Message under the Business Central code field, or null.
    /// </summary>
    [ObservableProperty]
    public partial string? BusinessCentralCodeError { get; set; }

    /// <summary>
    /// Opens the donor whose id is typed in <see cref="DonorIdText"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user leaves the page.</param>
    [RelayCommand]
    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        ClearErrors();
        StatusMessage = null;
        if (!Guid.TryParse(DonorIdText.Trim(), out var id))
        {
            DonorIdError = "Enter the id of a donor, such as 3f2504e0-4f89-11d3-9a0c-0305e82c3301.";
            return;
        }

        var result = await _apiClient.GetDonorAsync(id, cancellationToken);
        Show(result, "Donor opened.");
    }

    /// <summary>
    /// Empties the form to create a new donor.
    /// </summary>
    [RelayCommand]
    private void New()
    {
        ClearErrors();
        Fill(null);
        DonorIdText = string.Empty;
        StatusMessage = null;
    }

    /// <summary>
    /// Creates the donor, or saves the changes when a donor is open.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user leaves the page.</param>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ClearErrors();
        StatusMessage = null;
        var request = new DonorRequest(
            Name,
            Type,
            Address,
            City,
            Province,
            PostalCode,
            ContactPerson,
            Email,
            Phone,
            Notes,
            BusinessCentralCode);

        var result = CurrentDonorId is { } id
            ? await _apiClient.UpdateDonorAsync(id, request, cancellationToken)
            : await _apiClient.CreateDonorAsync(request, cancellationToken);
        Show(result, CurrentDonorId is null ? "Donor created." : "Donor saved.");
    }

    /// <summary>
    /// Shows the answer of the API: fills the form on success, or shows why the request failed.
    /// </summary>
    /// <param name="result">Answer of the API.</param>
    /// <param name="successMessage">Message shown when the request succeeded.</param>
    private void Show(DonorResult result, string successMessage)
    {
        switch (result.Status)
        {
            case DonorResultStatus.Success:
                Fill(result.Donor);
                DonorIdText = result.Donor!.Id.ToString();
                StatusMessage = successMessage;
                break;
            case DonorResultStatus.Invalid:
                foreach (var (field, messages) in result.Errors)
                {
                    SetError(field, string.Join(" ", messages));
                }

                StatusMessage = "Some fields need to be fixed.";
                break;
            case DonorResultStatus.NotFound:
                StatusMessage = "No donor has this id.";
                break;
            case DonorResultStatus.Conflict:
                BusinessCentralCodeError = "Another donor already uses this Business Central code.";
                StatusMessage = "Some fields need to be fixed.";
                break;
            case DonorResultStatus.Unauthorized:
                StatusMessage = "You must be signed in to manage donors.";
                break;
            case DonorResultStatus.Forbidden:
                StatusMessage = "Your role cannot create or edit donors.";
                break;
            default:
                StatusMessage = "Server unavailable";
                break;
        }
    }

    /// <summary>
    /// Puts a donor in the form, or empties the form when <paramref name="donor"/> is null.
    /// </summary>
    /// <param name="donor">Donor to show, or null for a new donor.</param>
    private void Fill(DonorDto? donor)
    {
        CurrentDonorId = donor?.Id;
        Name = donor?.Name ?? string.Empty;
        Type = donor?.Type;
        Address = donor?.Address ?? string.Empty;
        City = donor?.City ?? string.Empty;
        Province = donor?.Province ?? string.Empty;
        PostalCode = donor?.PostalCode ?? string.Empty;
        ContactPerson = donor?.ContactPerson ?? string.Empty;
        Email = donor?.Email ?? string.Empty;
        Phone = donor?.Phone ?? string.Empty;
        Notes = donor?.Notes ?? string.Empty;
        BusinessCentralCode = donor?.BusinessCentralCode ?? string.Empty;
    }

    /// <summary>
    /// Shows a message under the field the API named.
    /// </summary>
    /// <param name="field">Name of the field, as in <see cref="DonorRequest"/>.</param>
    /// <param name="message">Message to show.</param>
    private void SetError(string field, string message)
    {
        switch (field)
        {
            case nameof(DonorRequest.Name): NameError = message; break;
            case nameof(DonorRequest.Type): TypeError = message; break;
            case nameof(DonorRequest.Address): AddressError = message; break;
            case nameof(DonorRequest.City): CityError = message; break;
            case nameof(DonorRequest.Province): ProvinceError = message; break;
            case nameof(DonorRequest.PostalCode): PostalCodeError = message; break;
            case nameof(DonorRequest.ContactPerson): ContactPersonError = message; break;
            case nameof(DonorRequest.Email): EmailError = message; break;
            case nameof(DonorRequest.Phone): PhoneError = message; break;
            case nameof(DonorRequest.Notes): NotesError = message; break;
            case nameof(DonorRequest.BusinessCentralCode): BusinessCentralCodeError = message; break;
        }
    }

    /// <summary>
    /// Removes every field message.
    /// </summary>
    private void ClearErrors()
    {
        DonorIdError = null;
        NameError = null;
        TypeError = null;
        AddressError = null;
        CityError = null;
        ProvinceError = null;
        PostalCodeError = null;
        ContactPersonError = null;
        EmailError = null;
        PhoneError = null;
        NotesError = null;
        BusinessCentralCodeError = null;
    }
}
