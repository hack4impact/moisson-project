namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Types of donor, exactly as stored in the <c>type</c> column of the donors table.
/// An individual and an organization are named differently on a tax receipt.
/// </summary>
public static class DonorTypes
{
    /// <summary>
    /// A person who gives food.
    /// </summary>
    public const string Individual = "individual";

    /// <summary>
    /// A company or another organization that gives food.
    /// </summary>
    public const string Organization = "organization";

    /// <summary>
    /// Every type a donor can have.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Individual, Organization];
}
