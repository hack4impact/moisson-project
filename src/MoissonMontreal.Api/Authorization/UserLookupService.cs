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
    /// Microsoft account id of the last employee looked up during this request, or <c>null</c> before the first lookup.
    /// </summary>
    private Guid? _cachedObjectId;

    /// <summary>
    /// Result of the last lookup during this request, so the database is queried only once per employee and request.
    /// </summary>
    private User? _cachedUser;

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
    /// If the account is not linked yet, it is found by email and linked to the employee's Microsoft account.
    /// The result is kept for the rest of the request, so the database is queried only once.
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

        if (_cachedObjectId == objectId)
        {
            return _cachedUser;
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.EntraObjectId == objectId, cancellationToken)
            ?? await LinkByEmailAsync(principal, objectId, cancellationToken);

        _cachedObjectId = objectId;
        _cachedUser = user is { IsActive: true } ? user : null;
        return _cachedUser;
    }

    /// <summary>
    /// Finds an account that is not linked yet, by email, and saves the employee's Microsoft account id on it.
    /// </summary>
    /// <param name="principal">Signed-in employee, built from the validated access token.</param>
    /// <param name="objectId">Microsoft account id of the employee (<c>oid</c> claim).</param>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    /// <returns>The linked account with its role, or <c>null</c> if no account was added for this email.</returns>
    private async Task<User?> LinkByEmailAsync(ClaimsPrincipal principal, Guid objectId, CancellationToken cancellationToken)
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
