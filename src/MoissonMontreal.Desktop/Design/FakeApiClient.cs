using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Desktop.Services;

namespace MoissonMontreal.Desktop.Design;

/// <summary>
/// Fake API client for the IDE previewer. Always reports the API as online, without any network call.
/// </summary>
public class FakeApiClient : IApiClient
{
    /// <inheritdoc/>
    public Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<UserListResult> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var createdAt = new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.Zero);
        IReadOnlyList<UserDto> users =
        [
            new(1, "Admin Example", "admin@example.org", RoleCodes.Admin, true, createdAt, null),
            new(2, "Finance Example", "finance@example.org", RoleCodes.Finance, true, createdAt.AddDays(1), 1),
            new(3, "Supply Example", "supply@example.org", RoleCodes.Supply, false, createdAt.AddDays(2), 1),
        ];
        return Task.FromResult(new UserListResult(UserResultStatus.Success, users));
    }

    /// <inheritdoc/>
    public Task<UserResult> CreateUserAsync(UserRequest request, CancellationToken cancellationToken = default)
    {
        var user = new UserDto(
            4,
            request.DisplayName ?? string.Empty,
            request.Email ?? string.Empty,
            request.Role ?? string.Empty,
            true,
            DateTimeOffset.UtcNow,
            1);
        return Task.FromResult(new UserResult(UserResultStatus.Success, user, new Dictionary<string, string[]>()));
    }
}
