namespace MoissonMontreal.Core.Entities;

/// <summary>
/// Kind of change made to a Business Central item mapping.
/// </summary>
public enum BcItemMappingAction
{
    /// <summary>
    /// A person confirmed a new mapping.
    /// </summary>
    Created,

    /// <summary>
    /// A person pointed an existing mapping to another catalog item.
    /// </summary>
    Updated,

    /// <summary>
    /// A person removed a mapping.
    /// </summary>
    Deleted
}
