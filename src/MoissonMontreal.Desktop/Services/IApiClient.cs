using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Talks to the central Moisson Montréal API.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Checks whether the API and its database are ready, by calling the API's <c>/health</c> endpoint.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the check early (for example when leaving the page). If cancelled, the method returns <c>false</c>.</param>
    /// <returns><c>true</c> if <c>/health</c> answered 200 (API and database both work); <c>false</c> if the API is unreachable, the database is down (503), or any other error occurs.</returns>
    Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the signed-in employee and their role from the API's <c>api/me</c> endpoint.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The employee, or a failure: 403 when they have no active account, no status when the API could not be reached.</returns>
    Task<ApiResult<CurrentUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every account, active ones first. Administrators only.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The accounts, or a failure with a message.</returns>
    Task<ApiResult<List<AccountDto>>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the roles an account can be given. Administrators only.
    /// </summary>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The roles, or a failure with a message.</returns>
    Task<ApiResult<List<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an employee. Administrators only.
    /// </summary>
    /// <param name="request">Email, name and role of the employee.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The new account, or a failure with a message.</returns>
    Task<ApiResult<AccountDto>> CreateAccountAsync(AccountCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits the name and the email of an account. Administrators only.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="request">New email, new name and the version of the account shown on screen.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The edited account, or a failure with a message.</returns>
    Task<ApiResult<AccountDto>> UpdateAccountAsync(int id, AccountUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives an account another role. Administrators only.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="request">Code of the new role and the version of the account shown on screen.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The account with its new role, or a failure with a message.</returns>
    Task<ApiResult<AccountDto>> ChangeAccountRoleAsync(int id, AccountRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an employee's access. Administrators only.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="version">Version of the account shown on screen.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The deactivated account, or a failure with a message.</returns>
    Task<ApiResult<AccountDto>> DeactivateAccountAsync(int id, uint version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives an employee their access back. Administrators only.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="version">Version of the account shown on screen.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The reactivated account, or a failure with a message.</returns>
    Task<ApiResult<AccountDto>> ReactivateAccountAsync(int id, uint version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one donor.
    /// </summary>
    /// <param name="id">Identifier of the donor.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The donor, or the reason it could not be read.</returns>
    Task<DonorResult> GetDonorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a donor.
    /// </summary>
    /// <param name="request">Fields of the new donor.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The saved donor, or the reason it was refused.</returns>
    Task<DonorResult> CreateDonorAsync(DonorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the fields of a donor.
    /// </summary>
    /// <param name="id">Identifier of the donor.</param>
    /// <param name="request">New fields of the donor.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The saved donor, or the reason it was refused.</returns>
    Task<DonorResult> UpdateDonorAsync(Guid id, DonorRequest request, CancellationToken cancellationToken = default);
}
