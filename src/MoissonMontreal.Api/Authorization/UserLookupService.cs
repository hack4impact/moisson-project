using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Authorization;

/// <summary>
/// Finds the app account (users table) of the employee who sent the request.
/// </summary>
public class UserLookupService
{
    /// <summary>
    /// Database of the app.
    /// </summary>
    private readonly MoissonDbContext _dbContext;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="dbContext">Database of the app, one per request.</param>
    public UserLookupService(MoissonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Finds the active account of the signed-in employee, with its role.
    /// On the employee's first sign-in, the account is found by email and linked to their Microsoft account.
    /// </summary>
    /// <param name="principal">Signed-in employee, built from the validated access token.</param>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    /// <returns>The account with its role, or <c>null</c> if the employee has no account or their access was removed.</returns>
    public async Task<User?> FindActiveUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(principal.FindFirstValue("oid"), out var objectId))
        {
            return null;
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.EntraObjectId == objectId, cancellationToken)
            ?? await LinkOnFirstSignInAsync(principal, objectId, cancellationToken);

        return user is { IsActive: true } ? user : null;
    }

    /// <summary>
    /// Finds an account that was never signed in to, by email, and saves the employee's Microsoft account id on it.
    /// </summary>
    /// <param name="principal">Signed-in employee, built from the validated access token.</param>
    /// <param name="objectId">Microsoft account id of the employee (<c>oid</c> claim).</param>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    /// <returns>The linked account with its role, or <c>null</c> if no account was added for this email.</returns>
    private async Task<User?> LinkOnFirstSignInAsync(ClaimsPrincipal principal, Guid objectId, CancellationToken cancellationToken)
    {
        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue("preferred_username");
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail && u.EntraObjectId == null, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.EntraObjectId = objectId;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}
