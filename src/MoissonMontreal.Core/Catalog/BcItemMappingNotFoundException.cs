namespace MoissonMontreal.Core.Catalog;

/// <summary>
/// Thrown when a mapping is edited or deleted for a Business Central code that has no mapping.
/// </summary>
/// <param name="businessCentralCode">Code that has no mapping.</param>
public class BcItemMappingNotFoundException(string businessCentralCode)
    : Exception($"Business Central code {businessCentralCode} has no mapping.")
{
    /// <summary>
    /// Code that has no mapping.
    /// </summary>
    public string BusinessCentralCode { get; } = businessCentralCode;
}
