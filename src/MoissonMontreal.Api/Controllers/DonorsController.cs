using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Api.Donors;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using Npgsql;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Creates, shows and edits donor profiles.
/// Any signed-in employee can view a donor. Creating and editing a donor requires <see cref="Permissions.ManageDonors"/>.
/// </summary>
/// <param name="db">Database context of the request.</param>
[ApiController]
[Route("api/donors")]
public class DonorsController(MoissonDbContext db) : ControllerBase
{
    private static readonly Expression<Func<Donor, DonorDto>> AsDto = donor => new DonorDto(
        donor.Id,
        donor.Name,
        donor.Type,
        donor.Address,
        donor.City,
        donor.Province,
        donor.PostalCode,
        donor.ContactPerson,
        donor.Email,
        donor.Phone,
        donor.Notes,
        donor.BusinessCentralCode,
        donor.CreatedAt,
        donor.UpdatedAt);

    private static readonly Func<Donor, DonorDto> ToDto = AsDto.Compile();

    /// <summary>
    /// Returns one donor, or 404 if no donor has this id.
    /// </summary>
    /// <param name="id">Identifier of the donor.</param>
    /// <param name="cancellationToken">Stops the query when the request is cancelled.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DonorDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var donor = await db.Donors.SingleOrDefaultAsync(donor => donor.Id == id, cancellationToken);
        return donor is null ? NotFound() : ToDto(donor);
    }

    /// <summary>
    /// Adds a donor. Its Business Central code, if any, must be new.
    /// </summary>
    /// <param name="request">Fields of the new donor.</param>
    /// <param name="cancellationToken">Stops the save when the request is cancelled.</param>
    [HttpPost]
    [Authorize(Policy = Permissions.ManageDonors)]
    public async Task<ActionResult<DonorDto>> Create(DonorRequest request, CancellationToken cancellationToken)
    {
        if (!DonorValidator.TryNormalize(request, out var fields, out var errors))
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var donor = new Donor();
        Apply(donor, fields);
        db.Donors.Add(donor);

        if (await TrySaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return CreatedAtAction(nameof(Get), new { id = donor.Id }, ToDto(donor));
    }

    /// <summary>
    /// Replaces the fields of a donor, or returns 404 if no donor has this id, even when it is deleted during the edit.
    /// The Business Central code must stay unique. The date of the last change only moves when a field changes.
    /// </summary>
    /// <param name="id">Identifier of the donor.</param>
    /// <param name="request">New fields of the donor.</param>
    /// <param name="cancellationToken">Stops the save when the request is cancelled.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ManageDonors)]
    public async Task<ActionResult<DonorDto>> Update(Guid id, DonorRequest request, CancellationToken cancellationToken)
    {
        if (!DonorValidator.TryNormalize(request, out var fields, out var errors))
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var donor = await db.Donors.SingleOrDefaultAsync(donor => donor.Id == id, cancellationToken);
        if (donor is null)
        {
            return NotFound();
        }

        Apply(donor, fields);
        if (db.Entry(donor).State == EntityState.Modified)
        {
            donor.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (await TrySaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return ToDto(donor);
    }

    private static void Apply(Donor donor, DonorFields fields)
    {
        donor.Name = fields.Name;
        donor.Type = fields.Type;
        donor.Address = fields.Address;
        donor.City = fields.City;
        donor.Province = fields.Province;
        donor.PostalCode = fields.PostalCode;
        donor.ContactPerson = fields.ContactPerson;
        donor.Email = fields.Email;
        donor.Phone = fields.Phone;
        donor.Notes = fields.Notes;
        donor.BusinessCentralCode = fields.BusinessCentralCode;
    }

    /// <summary>
    /// Saves the changes, and turns the expected save errors into answers.
    /// </summary>
    /// <param name="cancellationToken">Stops the save when the request is cancelled.</param>
    /// <returns>Null when saved, 404 when the donor was deleted after it was read, or 409 when the Business Central code is already used.</returns>
    private async Task<ActionResult?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.ConstraintName == DonorConfiguration.BusinessCentralCodeIndex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Business Central code already used",
                Detail = "A donor with this Business Central code already exists.",
            });
        }
    }
}
