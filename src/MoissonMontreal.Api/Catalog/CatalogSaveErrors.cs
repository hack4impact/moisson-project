using Microsoft.EntityFrameworkCore;

using MoissonMontreal.Api.Data.Configurations;

using Npgsql;

namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Recognizes the database errors that can happen when catalog items are saved, so the catalog API and the import
/// answer the same way. These errors happen when another request changed the catalog between the checks and the save.
/// </summary>
public static class CatalogSaveErrors
{
    /// <summary>
    /// Tells whether a save failed because another catalog item already uses the same Business Central code.
    /// </summary>
    /// <param name="exception">Error thrown by SaveChangesAsync.</param>
    /// <returns>True if the unique index on the Business Central code refused the save.</returns>
    public static bool IsBusinessCentralCodeConflict(DbUpdateException exception) =>
        IsRefusedBy(exception, CatalogItemConfiguration.BusinessCentralCodeIndex);

    /// <summary>
    /// Tells whether a save failed because the quality class of a catalog item does not exist anymore.
    /// </summary>
    /// <param name="exception">Error thrown by SaveChangesAsync.</param>
    /// <returns>True if the foreign key to the quality classes refused the save.</returns>
    public static bool IsMissingQualityClass(DbUpdateException exception) =>
        IsRefusedBy(exception, CatalogItemConfiguration.QualityClassForeignKey);

    /// <summary>
    /// Tells whether a save failed because another mapping already uses the same Business Central code.
    /// </summary>
    /// <param name="exception">Error thrown by SaveChangesAsync.</param>
    /// <returns>True if the unique index on the mapping code refused the save.</returns>
    public static bool IsBcItemMappingCodeConflict(DbUpdateException exception) =>
        IsRefusedBy(exception, BcItemMappingConfiguration.BusinessCentralCodeIndex);

    /// <summary>
    /// Tells whether PostgreSQL refused a save because of a given constraint.
    /// </summary>
    /// <param name="exception">Error thrown by SaveChangesAsync.</param>
    /// <param name="constraintName">Name of the index or constraint, as created by the migrations.</param>
    /// <returns>True if that constraint refused the save.</returns>
    private static bool IsRefusedBy(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException postgres && postgres.ConstraintName == constraintName;
}
