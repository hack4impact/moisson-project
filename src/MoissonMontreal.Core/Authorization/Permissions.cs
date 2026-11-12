namespace MoissonMontreal.Core.Authorization;

/// <summary>
/// What a user can do in the app, and which roles are allowed to do it (from the PRD).
/// Each permission is also the name of an authorization policy in the API.
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Import donations from Business Central and link a donation to a donor.
    /// </summary>
    public const string ManageDonations = "ManageDonations";

    /// <summary>
    /// Add and update donors.
    /// </summary>
    public const string ManageDonors = "ManageDonors";

    /// <summary>
    /// Prepare a receipt, issue it to the donor once approved and generate its PDF.
    /// </summary>
    public const string ManageReceipts = "ManageReceipts";

    /// <summary>
    /// Approve or refuse a receipt, first level.
    /// </summary>
    public const string ApproveReceiptsLevel1 = "ApproveReceiptsLevel1";

    /// <summary>
    /// Approve or refuse a receipt, second level.
    /// </summary>
    public const string ApproveReceiptsLevel2 = "ApproveReceiptsLevel2";

    /// <summary>
    /// View the history.
    /// </summary>
    public const string ViewHistory = "ViewHistory";

    /// <summary>
    /// Export the data.
    /// </summary>
    public const string ExportData = "ExportData";

    /// <summary>
    /// Give employees access to the app, change their role and remove their access.
    /// </summary>
    public const string ManageAccounts = "ManageAccounts";

    /// <summary>
    /// Update the price grid.
    /// </summary>
    public const string ManagePriceGrid = "ManagePriceGrid";

    /// <summary>
    /// Update the quality percentages.
    /// </summary>
    public const string ManageQualityPercentages = "ManageQualityPercentages";

    /// <summary>
    /// Roles allowed for each permission. Supply Director has everything Supply has, and Admin has everything.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedRoles { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [ManageDonations] = [RoleCodes.Supply, RoleCodes.SupplyDirector, RoleCodes.Admin],
            [ManageDonors] = [RoleCodes.Supply, RoleCodes.SupplyDirector, RoleCodes.Admin],
            [ManageReceipts] = [RoleCodes.Supply, RoleCodes.SupplyDirector, RoleCodes.Admin],
            [ApproveReceiptsLevel1] = [RoleCodes.SupplyDirector, RoleCodes.Admin],
            [ApproveReceiptsLevel2] = [RoleCodes.Finance, RoleCodes.Admin],
            [ViewHistory] = [RoleCodes.Finance, RoleCodes.Admin],
            [ExportData] = [RoleCodes.Finance, RoleCodes.Admin],
            [ManageAccounts] = [RoleCodes.Admin],
            [ManagePriceGrid] = [RoleCodes.Admin],
            [ManageQualityPercentages] = [RoleCodes.Admin],
        };
}
