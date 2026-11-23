namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Organization or person who gives food to Moisson Montréal.
/// </summary>
public class Donor
{
    /// <summary>
    /// Longest name stored for a donor.
    /// </summary>
    public const int NameMaxLength = 255;

    /// <summary>
    /// Longest street address accepted by the API. The column itself has no limit.
    /// </summary>
    public const int AddressMaxLength = 255;

    /// <summary>
    /// Longest city stored for a donor.
    /// </summary>
    public const int CityMaxLength = 100;

    /// <summary>
    /// Longest province stored for a donor.
    /// </summary>
    public const int ProvinceMaxLength = 50;

    /// <summary>
    /// Longest postal code stored for a donor.
    /// </summary>
    public const int PostalCodeMaxLength = 20;

    /// <summary>
    /// Longest contact person stored for a donor.
    /// </summary>
    public const int ContactPersonMaxLength = 255;

    /// <summary>
    /// Longest email stored for a donor, the longest address an email server accepts.
    /// </summary>
    public const int EmailMaxLength = 254;

    /// <summary>
    /// Longest phone number stored for a donor.
    /// </summary>
    public const int PhoneMaxLength = 30;

    /// <summary>
    /// Longest notes accepted by the API. The column itself has no limit.
    /// </summary>
    public const int NotesMaxLength = 2000;

    /// <summary>
    /// Longest Business Central code stored for a donor.
    /// </summary>
    public const int BusinessCentralCodeMaxLength = 50;

    /// <summary>
    /// Identifier of the donor, assigned by the database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the donor: the full name of a person, or the name of an organization.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of donor, one of <see cref="DonorTypes"/>. Organization unless set otherwise.
    /// </summary>
    public string Type { get; set; } = DonorTypes.Organization;

    /// <summary>
    /// Street address of the donor.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City of the donor.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Province of the donor.
    /// </summary>
    public string? Province { get; set; }

    /// <summary>
    /// Postal code of the donor.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Person to contact at the donor.
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// Email of the donor.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Phone number of the donor.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Free notes about the donor.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Code of this donor in Business Central, or null if it has none. Two donors cannot share a code.
    /// </summary>
    public string? BusinessCentralCode { get; set; }

    /// <summary>
    /// Date and time the donor was added, set by the database.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Date and time of the last change, or null if the donor was never changed.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Food items given by this donor.
    /// </summary>
    public ICollection<FoodItem> FoodItems { get; set; } = [];
}
