namespace MoissonMontreal.Api.Catalog;

/// <summary>
/// Thrown when an import could not be saved because another request added a catalog item with one of the file's
/// Business Central codes at the same time, through the catalog API or another import. Nothing from the file is saved.
/// Importing the file again works, since that code now exists and its line becomes an update.
/// </summary>
/// <param name="innerException">Error thrown by the database when it refused the save.</param>
public class CatalogImportConflictException(Exception innerException)
    : Exception(
        "Another catalog item with a Business Central code from this file was saved at the same time. Nothing was imported. Import the file again.",
        innerException);
