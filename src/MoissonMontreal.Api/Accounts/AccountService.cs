using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Api.Data.Configurations;
using MoissonMontreal.Core.Accounts;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Entities;
using Npgsql;

namespace MoissonMontreal.Api.Accounts;

/// <summary>
/// Adds employees and changes their account: name and email, role, and access. An account is never deleted, so the history of
/// who did what stays attributed. Every change is saved together with its history row, and the app always keeps at least one
/// active administrator.
/// </summary>
/// <param name="db">Database context of the request.</param>
public class AccountService(MoissonDbContext db)
{
    /// <summary>
    /// Adds an employee. The account is linked to their Microsoft account the first time they sign in.
    /// </summary>
    /// <param name="email">Work email. Spaces around it are removed and it is stored in lowercase.</param>
    /// <param name="displayName">Name shown in the app. Spaces around it are removed.</param>
    /// <param name="roleCode">Code of the role, such as Supply.</param>
    /// <param name="adminUserId">Administrator who adds the employee.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The new account, with its role.</returns>
    /// <exception cref="ArgumentException">The email or the name is empty or too long.</exception>
    /// <exception cref="RoleNotFoundException">No role has this code.</exception>
    /// <exception cref="AccountEmailAlreadyUsedException">Another account already uses this email, even deactivated.</exception>
    public async Task<User> AddAsync(
        string email,
        string displayName,
        string roleCode,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedName = NormalizeDisplayName(displayName);
        var role = await FindRoleAsync(roleCode, cancellationToken);

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new AccountEmailAlreadyUsedException(normalizedEmail);
        }

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = normalizedName,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
        };
        db.Users.Add(user);
        db.UserAccountChanges.Add(new UserAccountChange
        {
            User = user,
            Action = UserAccountAction.Created,
            NewEmail = normalizedEmail,
            NewDisplayName = normalizedName,
            NewRoleId = role.Id,
            NewIsActive = true,
            ChangedByUserId = adminUserId,
        });

        await SaveAsync(user, normalizedEmail, cancellationToken);
        return user;
    }

    /// <summary>
    /// Edits the name and the email of an account. Only the values that changed are written to the history,
    /// and nothing is saved when neither changed.
    /// </summary>
    /// <param name="userId">Account to edit.</param>
    /// <param name="email">New work email. Spaces around it are removed and it is stored in lowercase.</param>
    /// <param name="displayName">New name. Spaces around it are removed.</param>
    /// <param name="expectedVersion">Version of the account when the administrator loaded it.</param>
    /// <param name="adminUserId">Administrator who edits the account.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The edited account, with its role.</returns>
    /// <exception cref="ArgumentException">The email or the name is empty or too long.</exception>
    /// <exception cref="AccountNotFoundException">No account has this identifier.</exception>
    /// <exception cref="AccountEditedMeanwhileException">Someone else changed the account since it was loaded.</exception>
    /// <exception cref="AccountEmailAlreadyUsedException">Another account already uses the new email.</exception>
    public async Task<User> UpdateAsync(
        int userId,
        string email,
        string displayName,
        uint expectedVersion,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedName = NormalizeDisplayName(displayName);
        var user = await FindAtVersionAsync(userId, expectedVersion, cancellationToken);

        var emailChanged = user.Email != normalizedEmail;
        var nameChanged = user.DisplayName != normalizedName;
        if (!emailChanged && !nameChanged)
        {
            return user;
        }

        if (emailChanged && await db.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId, cancellationToken))
        {
            throw new AccountEmailAlreadyUsedException(normalizedEmail);
        }

        db.UserAccountChanges.Add(new UserAccountChange
        {
            UserId = userId,
            Action = UserAccountAction.Updated,
            OldEmail = emailChanged ? user.Email : null,
            NewEmail = emailChanged ? normalizedEmail : null,
            OldDisplayName = nameChanged ? user.DisplayName : null,
            NewDisplayName = nameChanged ? normalizedName : null,
            ChangedByUserId = adminUserId,
        });
        user.Email = normalizedEmail;
        user.DisplayName = normalizedName;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await SaveAsync(user, normalizedEmail, cancellationToken);
        return user;
    }

    /// <summary>
    /// Gives an account another role. It takes effect on the employee's next request.
    /// </summary>
    /// <param name="userId">Account whose role changes.</param>
    /// <param name="roleCode">Code of the new role.</param>
    /// <param name="expectedVersion">Version of the account when the administrator loaded it.</param>
    /// <param name="adminUserId">Administrator who changes the role.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The account, with its new role.</returns>
    /// <exception cref="OwnAccountChangeException">The administrator tries to change their own role.</exception>
    /// <exception cref="RoleNotFoundException">No role has this code.</exception>
    /// <exception cref="AccountNotFoundException">No account has this identifier.</exception>
    /// <exception cref="AccountEditedMeanwhileException">Someone else changed the account since it was loaded.</exception>
    /// <exception cref="LastActiveAdminException">The account is the last active administrator.</exception>
    public async Task<User> ChangeRoleAsync(
        int userId,
        string roleCode,
        uint expectedVersion,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        RefuseOwnAccount(userId, adminUserId);
        var role = await FindRoleAsync(roleCode, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var adminRoleId = await LockActiveAdminsAndAccountAsync(userId, cancellationToken);
        var user = await FindAtVersionAsync(userId, expectedVersion, cancellationToken);

        if (user.RoleId == role.Id)
        {
            return user;
        }

        if (role.Id != adminRoleId)
        {
            await RefuseLosingLastActiveAdminAsync(user, adminRoleId, cancellationToken);
        }

        db.UserAccountChanges.Add(new UserAccountChange
        {
            UserId = userId,
            Action = UserAccountAction.RoleChanged,
            OldRoleId = user.RoleId,
            NewRoleId = role.Id,
            ChangedByUserId = adminUserId,
        });
        user.RoleId = role.Id;
        user.Role = role;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await SaveAsync(user, user.Email, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// Removes an employee's access. The account is kept, and the employee is refused from their next request.
    /// </summary>
    /// <param name="userId">Account to deactivate.</param>
    /// <param name="expectedVersion">Version of the account when the administrator loaded it.</param>
    /// <param name="adminUserId">Administrator who removes the access.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The deactivated account, with its role.</returns>
    /// <exception cref="OwnAccountChangeException">The administrator tries to remove their own access.</exception>
    /// <exception cref="AccountNotFoundException">No account has this identifier.</exception>
    /// <exception cref="AccountEditedMeanwhileException">Someone else changed the account since it was loaded.</exception>
    /// <exception cref="LastActiveAdminException">The account is the last active administrator.</exception>
    public async Task<User> DeactivateAsync(
        int userId,
        uint expectedVersion,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        RefuseOwnAccount(userId, adminUserId);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var adminRoleId = await LockActiveAdminsAndAccountAsync(userId, cancellationToken);
        var user = await FindAtVersionAsync(userId, expectedVersion, cancellationToken);

        if (!user.IsActive)
        {
            return user;
        }

        await RefuseLosingLastActiveAdminAsync(user, adminRoleId, cancellationToken);

        await SetActiveAsync(user, false, UserAccountAction.Deactivated, adminUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// Gives an employee their access back. What they created before stays attributed to them.
    /// </summary>
    /// <param name="userId">Account to reactivate.</param>
    /// <param name="expectedVersion">Version of the account when the administrator loaded it.</param>
    /// <param name="adminUserId">Administrator who gives the access back.</param>
    /// <param name="cancellationToken">Token that cancels the database queries.</param>
    /// <returns>The reactivated account, with its role.</returns>
    /// <exception cref="AccountNotFoundException">No account has this identifier.</exception>
    /// <exception cref="AccountEditedMeanwhileException">Someone else changed the account since it was loaded.</exception>
    public async Task<User> ReactivateAsync(
        int userId,
        uint expectedVersion,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindAtVersionAsync(userId, expectedVersion, cancellationToken);
        if (user.IsActive)
        {
            return user;
        }

        await SetActiveAsync(user, true, UserAccountAction.Reactivated, adminUserId, cancellationToken);
        return user;
    }

    /// <summary>
    /// Removes the spaces around an email, puts it in lowercase and refuses an empty, invalid or too long one.
    /// </summary>
    /// <param name="email">Email as received.</param>
    /// <returns>The email in lowercase, without spaces around it.</returns>
    /// <exception cref="ArgumentException">The email is empty, has no @ or is too long.</exception>
    private static string NormalizeEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || !normalized.Contains('@') || normalized.Length > User.EmailMaxLength)
        {
            throw new ArgumentException(
                $"The email must contain @ and be at most {User.EmailMaxLength} characters.", nameof(email));
        }

        return normalized;
    }

    /// <summary>
    /// Removes the spaces around a name and refuses an empty or too long one.
    /// </summary>
    /// <param name="displayName">Name as received.</param>
    /// <returns>The name without spaces around it.</returns>
    /// <exception cref="ArgumentException">The name is empty or too long.</exception>
    private static string NormalizeDisplayName(string displayName)
    {
        var normalized = (displayName ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Length > User.DisplayNameMaxLength)
        {
            throw new ArgumentException(
                $"The name is required and must be at most {User.DisplayNameMaxLength} characters.", nameof(displayName));
        }

        return normalized;
    }

    /// <summary>
    /// Refuses a change an administrator makes to their own role or access.
    /// </summary>
    /// <param name="userId">Account being changed.</param>
    /// <param name="adminUserId">Administrator making the change.</param>
    /// <exception cref="OwnAccountChangeException">Both are the same account.</exception>
    private static void RefuseOwnAccount(int userId, int adminUserId)
    {
        if (userId == adminUserId)
        {
            throw new OwnAccountChangeException(userId);
        }
    }

    /// <summary>
    /// Loads a role by its code.
    /// </summary>
    /// <param name="roleCode">Code of the role.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The role.</returns>
    /// <exception cref="RoleNotFoundException">No role has this code.</exception>
    private async Task<Role> FindRoleAsync(string roleCode, CancellationToken cancellationToken) =>
        await db.Roles.SingleOrDefaultAsync(r => r.Code == roleCode, cancellationToken)
        ?? throw new RoleNotFoundException(roleCode);

    /// <summary>
    /// Loads an account with its role, and checks it has not changed since the administrator loaded it.
    /// The version is also given to Entity Framework, so a change made between this check and the save is refused too.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="expectedVersion">Version of the account when the administrator loaded it.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>The account, tracked by Entity Framework.</returns>
    /// <exception cref="AccountNotFoundException">No account has this identifier.</exception>
    /// <exception cref="AccountEditedMeanwhileException">The version is different.</exception>
    private async Task<User> FindAtVersionAsync(int userId, uint expectedVersion, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AccountNotFoundException(userId);

        if (user.Version != expectedVersion)
        {
            throw new AccountEditedMeanwhileException(userId);
        }

        db.Entry(user).Property(u => u.Version).OriginalValue = expectedVersion;
        return user;
    }

    /// <summary>
    /// Locks the rows of the active administrators and of the account, until the transaction ends.
    /// Two changes that could remove the last administrator at the same time then run one after the other,
    /// and the second one sees the result of the first.
    /// </summary>
    /// <param name="userId">Account being changed.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <returns>Identifier of the administrator role.</returns>
    private async Task<int> LockActiveAdminsAndAccountAsync(int userId, CancellationToken cancellationToken)
    {
        var adminRoleId = await db.Roles.Where(r => r.Code == RoleCodes.Admin).Select(r => r.Id).SingleAsync(cancellationToken);

        // Only the id is selected: SELECT * would not return the xmin column used as the version.
        await db.Database.ExecuteSqlAsync(
            $"SELECT id FROM users WHERE (role_id = {adminRoleId} AND is_active) OR id = {userId} ORDER BY id FOR UPDATE",
            cancellationToken);

        return adminRoleId;
    }

    /// <summary>
    /// Refuses a change that would make an active administrator lose the role or the access when no other active
    /// administrator exists.
    /// </summary>
    /// <param name="user">Account being changed.</param>
    /// <param name="adminRoleId">Identifier of the administrator role.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    /// <exception cref="LastActiveAdminException">The account is the last active administrator.</exception>
    private async Task RefuseLosingLastActiveAdminAsync(User user, int adminRoleId, CancellationToken cancellationToken)
    {
        if (!user.IsActive || user.RoleId != adminRoleId)
        {
            return;
        }

        var otherActiveAdmins = await db.Users.CountAsync(
            u => u.Id != user.Id && u.IsActive && u.RoleId == adminRoleId,
            cancellationToken);

        if (otherActiveAdmins == 0)
        {
            throw new LastActiveAdminException(user.Id);
        }
    }

    /// <summary>
    /// Changes the access of an account and writes the history row.
    /// </summary>
    /// <param name="user">Account to change, tracked by Entity Framework.</param>
    /// <param name="isActive">New access.</param>
    /// <param name="action">Deactivated or Reactivated.</param>
    /// <param name="adminUserId">Administrator making the change.</param>
    /// <param name="cancellationToken">Token that cancels the database query.</param>
    private async Task SetActiveAsync(
        User user,
        bool isActive,
        UserAccountAction action,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        db.UserAccountChanges.Add(new UserAccountChange
        {
            UserId = user.Id,
            Action = action,
            OldIsActive = user.IsActive,
            NewIsActive = isActive,
            ChangedByUserId = adminUserId,
        });
        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await SaveAsync(user, user.Email, cancellationToken);
    }

    /// <summary>
    /// Saves the pending changes and turns the database refusals into clear errors.
    /// </summary>
    /// <param name="user">Account being saved.</param>
    /// <param name="email">Email being saved, used in the error when it is already used.</param>
    /// <param name="cancellationToken">Token that cancels the save.</param>
    /// <exception cref="AccountEditedMeanwhileException">Someone else changed the account between the check and the save.</exception>
    /// <exception cref="AccountEmailAlreadyUsedException">Another account saved the same email between the check and the save.</exception>
    private async Task SaveAsync(User user, string email, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AccountEditedMeanwhileException(user.Id);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { ConstraintName: UserConfiguration.EmailIndex })
        {
            throw new AccountEmailAlreadyUsedException(email);
        }
    }
}
