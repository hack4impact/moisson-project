namespace MoissonMontreal.Core.Catalog;

/// <summary>
/// Thrown when a mapping is created for a Business Central code that already has one, because a code can only have one mapping.
/// </summary>
/// <param name="businessCentralCode">Code that already has a mapping.</param>
public class BcItemMappingConflictException(string businessCentralCode)
    : Exception($"Business Central code {businessCentralCode} already has a mapping.")
{
    /// <summary>
    /// Code that already has a mapping.
    /// </summary>
    public string BusinessCentralCode { get; } = businessCentralCode;
}
