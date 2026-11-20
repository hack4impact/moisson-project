using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MoissonMontreal.Api.Catalog;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Imports the food catalog from an Excel export of Business Central.
/// It sits next to <see cref="CatalogItemsController"/> and requires the same permission to change the catalog,
/// <see cref="Permissions.ManagePriceGrid"/>.
/// </summary>
/// <param name="importer">Saves the rows of the export to the catalog.</param>
[ApiController]
[Route("api/catalog-items/import")]
public class CatalogImportController(CatalogImporter importer) : ControllerBase
{
    /// <summary>
    /// Largest file accepted, 10 MB. A Business Central PO export of a few dozen lines is about 15 KB.
    /// </summary>
    public const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Largest request accepted: the file plus room for the rest of the form.
    /// </summary>
    private const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;

    /// <summary>
    /// Extension of the only file type accepted.
    /// </summary>
    private const string ExcelExtension = ".xlsx";

    /// <summary>
    /// Reads an Excel export of Business Central and creates or updates the catalog items it contains.
    /// Invalid lines are listed in the report, sorted by row number, and do not stop the others.
    /// </summary>
    /// <param name="file">The .xlsx file, sent as a form field named file.</param>
    /// <param name="cancellationToken">Token that cancels the import.</param>
    /// <returns>
    /// 200 with the number of items created, updated and unchanged, and the refused lines.
    /// 400 when no file is sent, or when the file is too large, is not an .xlsx file, or misses a required sheet or column.
    /// 409 when another request added an item with one of the file's codes at the same time; nothing is saved and the file can be imported again.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Permissions.ManagePriceGrid)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<CatalogImportReport>> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return FileProblem("No file was sent. Send the Business Central export in a form field named file.");
        }

        if (file.Length > MaxFileBytes)
        {
            return FileProblem($"The file is too large. The limit is {MaxFileBytes / (1024 * 1024)} MB.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ExcelExtension, StringComparison.OrdinalIgnoreCase))
        {
            return FileProblem($"The file must be an Excel file ({ExcelExtension}).");
        }

        BusinessCentralExport export;
        try
        {
            await using var stream = file.OpenReadStream();
            export = BusinessCentralExportReader.Read(stream);
        }
        catch (BusinessCentralExportException exception)
        {
            return FileProblem(exception.Message);
        }

        CatalogImportReport report;
        try
        {
            report = await importer.ImportAsync(export.Rows, cancellationToken);
        }
        catch (CatalogImportConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Business Central code already used",
                Detail = exception.Message,
            });
        }

        var errors = export.Errors
            .Concat(report.Errors)
            .OrderBy(error => error.RowNumber)
            .ToList();

        return report with { Errors = errors };
    }

    /// <summary>
    /// Builds a 400 answer that explains why the whole file was refused.
    /// </summary>
    /// <param name="detail">What is wrong with the file.</param>
    /// <returns>A 400 Bad Request with a problem description.</returns>
    private BadRequestObjectResult FileProblem(string detail) => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "The file cannot be imported",
        Detail = detail,
    });
}
