using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Desktop.Services;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;

namespace MoissonMontreal.Desktop.Design;

/// <summary>
/// Fake API client for the IDE previewer. Always reports the API as online and returns sample data, without any network call.
/// </summary>
public class FakeApiClient : IApiClient
{
    /// <summary>
    /// Sample account shown in the previewer.
    /// </summary>
    private static readonly AccountDto SampleAccount = new(
        2, "jean.tremblay@moissonmontreal.org", "Jean Tremblay", RoleCodes.Supply, "Supply",
        true, true, DateTimeOffset.UtcNow, null, 1);

    /// <inheritdoc/>
    public Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<ApiResult<CurrentUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = new CurrentUserDto(
            ObjectId: "00000000-0000-0000-0000-000000000000",
            Name: "Preview User",
            Email: "preview@example.org",
            TenantId: null,
            Role: RoleCodes.Admin);
        return Task.FromResult(ApiResult<CurrentUserDto>.Ok(user, HttpStatusCode.OK));
    }

    /// <inheritdoc/>
    public Task<ApiResult<List<AccountDto>>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        List<AccountDto> accounts =
        [
            new(1, "preview@example.org", "Preview User", RoleCodes.Admin, "Admin", true, true, DateTimeOffset.UtcNow, null, 1),
            SampleAccount,
            new(3, "former.employee@moissonmontreal.org", "Former Employee", RoleCodes.Finance, "Finance", false, true, DateTimeOffset.UtcNow, null, 1),
        ];
        return Task.FromResult(ApiResult<List<AccountDto>>.Ok(accounts, HttpStatusCode.OK));
    }

    /// <inheritdoc/>
    public Task<ApiResult<List<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        List<RoleDto> roles =
        [
            new(RoleCodes.Supply, "Supply"),
            new(RoleCodes.SupplyDirector, "Supply Director"),
            new(RoleCodes.Finance, "Finance"),
            new(RoleCodes.Admin, "Admin"),
        ];
        return Task.FromResult(ApiResult<List<RoleDto>>.Ok(roles, HttpStatusCode.OK));
    }

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> CreateAccountAsync(AccountCreateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<AccountDto>.Ok(SampleAccount, HttpStatusCode.Created));

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> UpdateAccountAsync(int id, AccountUpdateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<AccountDto>.Ok(SampleAccount, HttpStatusCode.OK));

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> ChangeAccountRoleAsync(int id, AccountRoleRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<AccountDto>.Ok(SampleAccount, HttpStatusCode.OK));

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> DeactivateAccountAsync(int id, uint version, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<AccountDto>.Ok(SampleAccount, HttpStatusCode.OK));

    /// <inheritdoc/>
    public Task<ApiResult<AccountDto>> ReactivateAccountAsync(int id, uint version, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult<AccountDto>.Ok(SampleAccount, HttpStatusCode.OK));
}
