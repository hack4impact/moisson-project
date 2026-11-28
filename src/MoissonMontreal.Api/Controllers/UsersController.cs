using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Authorization;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Api.Users;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using Npgsql;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Lists and creates the accounts of the employees who can use the app. Every action requires <see cref="Permissions.ManageAccounts"/>.
/// Accounts have no password: employees sign in with their Microsoft work account.
/// </summary>
/// <param name="db">Database context of the request.</param>
/// <param name="userLookup">Finds the account of the admin who sends the request.</param>
/// <param name="logger">Records each account creation with its author.</param>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Permissions.ManageAccounts)]
public class UsersController(MoissonDbContext db, UserLookupService userLookup, ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>
    /// Turns an account into what the API returns.
    /// </summary>
    private static readonly Expression<Func<User, UserDto>> AsDto = user => new UserDto(
        user.Id,
        user.DisplayName,
        user.Email,
        user.Role.Code,
        user.IsActive,
        user.CreatedAt,
        user.CreatedById);

    /// <summary>
    /// Compiled version of <see cref="AsDto"/>, for an account already loaded.
    /// </summary>
    private static readonly Func<User, UserDto> ToDto = AsDto.Compile();

    /// <summary>
    /// Returns every account, active or not, ordered by name then email.
    /// </summary>
    /// <param name="cancellationToken">Stops the query when the request is cancelled.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Email)
            .Select(AsDto)
            .ToListAsync(cancellationToken);

        return users;
    }

    /// <summary>
    /// Creates an account with a name, an email and a role, and records the admin who created it.
    /// The email must not be used by another account, whatever its capital letters.
    /// </summary>
    /// <param name="request">Fields of the new account.</param>
    /// <param name="cancellationToken">Stops the save when the request is cancelled.</param>
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(UserRequest request, CancellationToken cancellationToken)
    {
        if (!UserValidator.TryNormalize(request, out var fields, out var errors))
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var author = await userLookup.FindActiveUserAsync(User, cancellationToken);
        if (author is null)
        {
            return Forbid();
        }

        var role = await db.Roles.SingleAsync(role => role.Code == fields.Role, cancellationToken);
        var user = new User
        {
            DisplayName = fields.DisplayName,
            Email = fields.Email,
            Role = role,
            CreatedById = author.Id,
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.ConstraintName == UserConfiguration.EmailIndex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already used",
                Detail = "An account with this email already exists.",
            });
        }

        logger.LogInformation("Account {UserId} created by account {CreatedById}", user.Id, author.Id);
        return StatusCode(StatusCodes.Status201Created, ToDto(user));
    }
}
