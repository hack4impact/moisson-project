using System.Net.Mail;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Users;

/// <summary>
/// Checks an account request and returns the values ready to store.
/// </summary>
public static class UserValidator
{
    /// <summary>
    /// Trims each field, puts the email in lowercase and reports every problem, with one message per field.
    /// A true result means <paramref name="fields"/> can be saved.
    /// </summary>
    /// <param name="request">Fields sent by the client.</param>
    /// <param name="fields">Trimmed fields, ready to store when the result is true.</param>
    /// <param name="errors">One message per field that has a problem, keyed by the field name.</param>
    /// <returns>True when the request has no problem.</returns>
    public static bool TryNormalize(UserRequest request, out UserFields fields, out Dictionary<string, string[]> errors)
    {
        var found = new Dictionary<string, string[]>();
        var displayName = Required(request.DisplayName, nameof(UserRequest.DisplayName), User.DisplayNameMaxLength, found);
        var email = Email(request.Email, found);
        var role = Role(request.Role, found);

        fields = new UserFields(displayName, email, role);
        errors = found;
        return found.Count == 0;
    }

    /// <summary>
    /// Trims a required field and records a message when it is empty or too long.
    /// </summary>
    /// <param name="value">Value sent by the client.</param>
    /// <param name="field">Name of the field, used as the key of the message.</param>
    /// <param name="maxLength">Maximum number of characters.</param>
    /// <param name="errors">Messages found so far.</param>
    /// <returns>The trimmed value.</returns>
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

    /// <summary>
    /// Trims the email, puts it in lowercase and records a message when it is missing, too long or not a valid address.
    /// </summary>
    /// <param name="value">Email sent by the client.</param>
    /// <param name="errors">Messages found so far.</param>
    /// <returns>The email, trimmed and in lowercase.</returns>
    private static string Email(string? value, Dictionary<string, string[]> errors)
    {
        var email = Required(value, nameof(UserRequest.Email), User.EmailMaxLength, errors).ToLowerInvariant();
        if (errors.ContainsKey(nameof(UserRequest.Email)))
        {
            return email;
        }

        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            errors[nameof(UserRequest.Email)] = ["Email must be a valid email address."];
        }

        return email;
    }

    /// <summary>
    /// Trims the role and records a message when it is not exactly one of the four role codes.
    /// </summary>
    /// <param name="value">Role code sent by the client.</param>
    /// <param name="errors">Messages found so far.</param>
    /// <returns>The trimmed role code.</returns>
    private static string Role(string? value, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!RoleCodes.All.Contains(trimmed))
        {
            errors[nameof(UserRequest.Role)] = [$"Role must be one of: {string.Join(", ", RoleCodes.All)}."];
        }

        return trimmed;
    }
}

/// <summary>
/// Account fields that passed validation. The email is trimmed and in lowercase.
/// </summary>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="Email">Work email, in lowercase.</param>
/// <param name="Role">Role code.</param>
public readonly record struct UserFields(string DisplayName, string Email, string Role);
