using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Accounts;
using MoissonMontreal.Api.Authorization;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Accounts;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Controllers;

/// <summary>
/// Lets an administrator list employees' accounts, add them, edit them, change their role and remove or give back their access.
/// Every route requires <see cref="Permissions.ManageAccounts"/>, so every rule is enforced on the server.
/// </summary>
/// <param name="db">Database context of the request.</param>
/// <param name="accounts">Service that changes the accounts and writes their history.</param>
/// <param name="userLookup">Service that finds the account of the signed-in administrator.</param>
[ApiController]
[Route("api/accounts")]
[Authorize(Policy = Permissions.ManageAccounts)]
public class AccountsController(
    MoissonDbContext db,
    AccountService accounts,
    UserLookupService userLookup) : ControllerBase
{
    /// <summary>
    /// Turns an account into the shape returned by the API, inside the database query.
    /// </summary>
    private static readonly Expression<Func<User, AccountDto>> AsDto = u => new AccountDto(
        u.Id,
        u.Email,
        u.DisplayName,
        u.Role.Code,
        u.Role.Name,
        u.IsActive,
        u.EntraObjectId != null,
        u.CreatedAt,
        u.UpdatedAt,
        u.Version);

    /// <summary>
    /// Same as <see cref="AsDto"/>, for an account already loaded with its role.
    /// </summary>
    private static readonly Func<User, AccountDto> ToDto = AsDto.Compile();

    /// <summary>
    /// Lists every account: active ones first, then deactivated ones, each sorted by name.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The accounts.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> List(CancellationToken cancellationToken) =>
        await db.Users
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.DisplayName)
            .Select(AsDto)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Lists the roles an account can be given.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The roles, in the order of their identifier.</returns>
    [HttpGet("roles")]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles(CancellationToken cancellationToken) =>
        await db.Roles
            .OrderBy(r => r.Id)
            .Select(r => new RoleDto(r.Code, r.Name))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns one account.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The account, or 404.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> Get(int id, CancellationToken cancellationToken)
    {
        var account = await db.Users.Where(u => u.Id == id).Select(AsDto).SingleOrDefaultAsync(cancellationToken);
        return account is null ? AccountNotFound(new AccountNotFoundException(id)) : account;
    }

    /// <summary>
    /// Returns every change made to an account, newest first.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The changes, or 404 when the account does not exist.</returns>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<AccountChangeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AccountChangeDto>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, cancellationToken))
        {
            return AccountNotFound(new AccountNotFoundException(id));
        }

        return await db.UserAccountChanges
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.ChangedAt)
            .Select(c => new AccountChangeDto(
                c.Id,
                c.Action.ToString(),
                c.OldEmail,
                c.NewEmail,
                c.OldDisplayName,
                c.NewDisplayName,
                db.Roles.Where(r => r.Id == c.OldRoleId).Select(r => r.Code).FirstOrDefault(),
                db.Roles.Where(r => r.Id == c.NewRoleId).Select(r => r.Code).FirstOrDefault(),
                c.OldIsActive,
                c.NewIsActive,
                c.ChangedByUserId,
                c.ChangedBy.DisplayName,
                c.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds an employee. They can sign in as soon as the account exists.
    /// </summary>
    /// <param name="request">Email, name and role.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>201 with the account, 400 when a field is missing or invalid, or 409 when the email is already used.</returns>
    [HttpPost]
    [ProducesResponseType<AccountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDto>> Create(AccountCreateRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        RequireText(errors, nameof(request.Email), request.Email);
        RequireText(errors, nameof(request.DisplayName), request.DisplayName);
        RequireText(errors, nameof(request.RoleCode), request.RoleCode);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return await RunAsync(async admin =>
        {
            var user = await accounts.AddAsync(
                request.Email!, request.DisplayName!, request.RoleCode!, admin.Id, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = user.Id }, ToDto(user));
        }, cancellationToken);
    }

    /// <summary>
    /// Edits the name and the email of an account.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="request">New email, new name and the version the administrator loaded.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>200 with the account, 400, 404, or 409 when the email is used or the account changed in the meantime.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDto>> Update(int id, AccountUpdateRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        RequireText(errors, nameof(request.Email), request.Email);
        RequireText(errors, nameof(request.DisplayName), request.DisplayName);
        RequireVersion(errors, request.Version);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return await RunAsync(async admin => ToDto(await accounts.UpdateAsync(
            id, request.Email!, request.DisplayName!, request.Version!.Value, admin.Id, cancellationToken)), cancellationToken);
    }

    /// <summary>
    /// Gives an account another role. It takes effect on the employee's next request.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="request">Code of the new role and the version the administrator loaded.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>200 with the account, 400 for an unknown role or the administrator's own account, 404,
    /// or 409 when the account changed in the meantime or is the last active administrator.</returns>
    [HttpPut("{id:int}/role")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDto>> ChangeRole(int id, AccountRoleRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        RequireText(errors, nameof(request.RoleCode), request.RoleCode);
        RequireVersion(errors, request.Version);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return await RunAsync(async admin => ToDto(await accounts.ChangeRoleAsync(
            id, request.RoleCode!, request.Version!.Value, admin.Id, cancellationToken)), cancellationToken);
    }

    /// <summary>
    /// Removes an employee's access. The account is kept and the employee is refused from their next request.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="request">Version the administrator loaded.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>200 with the account, 400 for the administrator's own account, 404,
    /// or 409 when the account changed in the meantime or is the last active administrator.</returns>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDto>> Deactivate(int id, AccountVersionRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        RequireVersion(errors, request.Version);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return await RunAsync(async admin => ToDto(await accounts.DeactivateAsync(
            id, request.Version!.Value, admin.Id, cancellationToken)), cancellationToken);
    }

    /// <summary>
    /// Gives an employee their access back.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="request">Version the administrator loaded.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>200 with the account, 400, 404, or 409 when the account changed in the meantime.</returns>
    [HttpPost("{id:int}/reactivate")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDto>> Reactivate(int id, AccountVersionRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        RequireVersion(errors, request.Version);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return await RunAsync(async admin => ToDto(await accounts.ReactivateAsync(
            id, request.Version!.Value, admin.Id, cancellationToken)), cancellationToken);
    }

    /// <summary>
    /// Adds an error when a text field is missing or blank.
    /// </summary>
    /// <param name="errors">Errors collected for the request.</param>
    /// <param name="field">Name of the field.</param>
    /// <param name="value">Value received.</param>
    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
    }

    /// <summary>
    /// Adds an error when the version is missing.
    /// </summary>
    /// <param name="errors">Errors collected for the request.</param>
    /// <param name="version">Version received.</param>
    private static void RequireVersion(Dictionary<string, string[]> errors, uint? version)
    {
        if (version is null)
        {
            errors["Version"] = ["Version is required: send the version of the account you loaded."];
        }
    }

    /// <summary>
    /// Finds the signed-in administrator, runs a change, and turns the service errors into HTTP answers.
    /// </summary>
    /// <param name="change">Change to run with the administrator's account.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The answer of the change, or the matching error.</returns>
    private async Task<ActionResult<AccountDto>> RunAsync(
        Func<User, Task<ActionResult<AccountDto>>> change,
        CancellationToken cancellationToken)
    {
        if (await userLookup.FindActiveUserAsync(User, cancellationToken) is not { } admin)
        {
            return Forbid();
        }

        try
        {
            return await change(admin);
        }
        catch (ArgumentException e)
        {
            var field = e.ParamName == "email" ? "Email" : "DisplayName";
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [field] = [e.Message.Replace($" (Parameter '{e.ParamName}')", string.Empty)],
            }));
        }
        catch (RoleNotFoundException e)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["RoleCode"] = [e.Message],
            }));
        }
        catch (OwnAccountChangeException e)
        {
            return Error(StatusCodes.Status400BadRequest, "Own account", e.Message);
        }
        catch (AccountNotFoundException e)
        {
            return AccountNotFound(e);
        }
        catch (AccountEmailAlreadyUsedException e)
        {
            return Error(StatusCodes.Status409Conflict, "Email already used", e.Message);
        }
        catch (AccountEditedMeanwhileException e)
        {
            return Error(StatusCodes.Status409Conflict, "Account changed by someone else", e.Message);
        }
        catch (LastActiveAdminException e)
        {
            return Error(StatusCodes.Status409Conflict, "Last active administrator", e.Message);
        }
    }

    /// <summary>
    /// Builds an error answer with a status, a short title and the detail shown to the administrator.
    /// </summary>
    /// <param name="status">HTTP status code.</param>
    /// <param name="title">Short title of the error.</param>
    /// <param name="detail">Message shown to the administrator.</param>
    /// <returns>The error answer.</returns>
    private ObjectResult Error(int status, string title, string detail) =>
        new(ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: title, detail: detail)) { StatusCode = status };

    /// <summary>
    /// Builds the 404 answer for an account that does not exist.
    /// </summary>
    /// <param name="e">Error describing the missing account.</param>
    /// <returns>A 404 with the account identifier.</returns>
    private NotFoundObjectResult AccountNotFound(AccountNotFoundException e)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, StatusCodes.Status404NotFound, title: "Account not found", detail: e.Message);
        problem.Extensions["userId"] = e.UserId;
        return NotFound(problem);
    }
}
