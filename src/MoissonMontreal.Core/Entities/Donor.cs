namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Organization or person who gives food to Moisson Montréal.
/// </summary>
public class Donor
{
    /// <summary>
    /// Identifier of the donor.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the donor.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Street address of the donor.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Postal code of the donor.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Person to contact at the donor.
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// When the donor was recorded. The database sets this when it is left empty.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Food items given by this donor.
    /// </summary>
    public ICollection<FoodItem> FoodItems { get; set; } = [];
}
