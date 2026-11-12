namespace MoissonMontreal.Core.Authorization;

/// <summary>
/// Codes of the four roles, exactly as stored in the <c>code</c> column of the roles table.
/// Use these constants instead of typing the codes by hand.
/// </summary>
public static class RoleCodes
{
    /// <summary>
    /// Supply team: imports donations, manages donors, prepares and issues receipts.
    /// </summary>
    public const string Supply = "Supply";

    /// <summary>
    /// Supply director: everything Supply can do, plus the first-level approval of receipts.
    /// </summary>
    public const string SupplyDirector = "SupplyDirector";

    /// <summary>
    /// Finance team: second-level approval of receipts, history and data export.
    /// </summary>
    public const string Finance = "Finance";

    /// <summary>
    /// Administrator: everything, plus accounts, the price grid and the quality percentages.
    /// </summary>
    public const string Admin = "Admin";
}
