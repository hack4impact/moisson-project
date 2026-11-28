using System.Net.Mail;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Donors;

/// <summary>
/// Checks a donor request and returns the values ready to store.
/// </summary>
public static class DonorValidator
{
    /// <summary>
    /// Trims each field and reports every problem, with one message per field. A true result means <paramref name="fields"/> can be saved.
    /// Name, type, address, city, province and postal code are required. A blank optional field becomes null.
    /// </summary>
    /// <param name="request">Fields sent by the client.</param>
    /// <param name="fields">Trimmed fields, ready to store when the result is true.</param>
    /// <param name="errors">One message per field that has a problem, keyed by the field name.</param>
    /// <returns>True when the request has no problem.</returns>
    public static bool TryNormalize(DonorRequest request, out DonorFields fields, out Dictionary<string, string[]> errors)
    {
        var found = new Dictionary<string, string[]>();
        var name = Required(request.Name, nameof(DonorRequest.Name), Donor.NameMaxLength, found);
        var type = Type(request.Type, found);
        var address = Required(request.Address, nameof(DonorRequest.Address), Donor.AddressMaxLength, found);
        var city = Required(request.City, nameof(DonorRequest.City), Donor.CityMaxLength, found);
        var province = Required(request.Province, nameof(DonorRequest.Province), Donor.ProvinceMaxLength, found);
        var postalCode = Required(request.PostalCode, nameof(DonorRequest.PostalCode), Donor.PostalCodeMaxLength, found);
        var contactPerson = Optional(request.ContactPerson, nameof(DonorRequest.ContactPerson), Donor.ContactPersonMaxLength, found);
        var email = Email(request.Email, found);
        var phone = Optional(request.Phone, nameof(DonorRequest.Phone), Donor.PhoneMaxLength, found);
        var notes = Optional(request.Notes, nameof(DonorRequest.Notes), Donor.NotesMaxLength, found);
        var code = Optional(request.BusinessCentralCode, nameof(DonorRequest.BusinessCentralCode), Donor.BusinessCentralCodeMaxLength, found);

        fields = new DonorFields(name, type, address, city, province, postalCode, contactPerson, email, phone, notes, code);
        errors = found;
        return found.Count == 0;
    }

    private static string Required(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors[field] = [$"{field} is required."];
        }
        else if (trimmed.Length > maxLength)
        {
            errors[field] = [$"{field} must be {maxLength} characters or fewer."];
        }

        return trimmed;
    }

    private static string? Optional(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors[field] = [$"{field} must be {maxLength} characters or fewer."];
        }

        return trimmed;
    }

    private static string Type(string? value, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!DonorTypes.All.Contains(trimmed))
        {
            errors[nameof(DonorRequest.Type)] = [$"Type must be {DonorTypes.Individual} or {DonorTypes.Organization}."];
        }

        return trimmed;
    }

    private static string? Email(string? value, Dictionary<string, string[]> errors)
    {
        var email = Optional(value, nameof(DonorRequest.Email), Donor.EmailMaxLength, errors);
        if (email is null || errors.ContainsKey(nameof(DonorRequest.Email)))
        {
            return email;
        }

        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            errors[nameof(DonorRequest.Email)] = ["Email must be a valid email address."];
        }

        return email;
    }
}

/// <summary>
/// Trimmed donor fields that passed validation.
/// </summary>
public readonly record struct DonorFields(
    string Name,
    string Type,
    string Address,
    string City,
    string Province,
    string PostalCode,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Notes,
    string? BusinessCentralCode);
