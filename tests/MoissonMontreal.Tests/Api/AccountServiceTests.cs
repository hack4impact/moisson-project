using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using MoissonMontreal.Api.Accounts;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Accounts;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class AccountServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Add_SavesTheAccountInLowercase_AndItsHistoryWithTheAuthor()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var email = NewEmail();

        await using (var context = CreateContext())
        {
            var user = await new AccountService(context).AddAsync($"  {email.ToUpperInvariant()} ", " Jean Tremblay ", RoleCodes.Supply, admin.Id);
            Assert.Equal((email, "Jean Tremblay", RoleCodes.Supply, true, null), (user.Email, user.DisplayName, user.Role.Code, user.IsActive, user.EntraObjectId));
            Assert.NotEqual(0u, user.Version);
        }

        var created = await FindByEmailAsync(email);
        var change = Assert.Single(await HistoryAsync(created.Id));
        Assert.Equal(UserAccountAction.Created, change.Action);
        Assert.Equal((email, "Jean Tremblay", created.RoleId, true), (change.NewEmail, change.NewDisplayName, change.NewRoleId, change.NewIsActive));
        Assert.Equal((null, null, null, null), (change.OldEmail, change.OldDisplayName, change.OldRoleId, change.OldIsActive));
        Assert.Equal(admin.Id, change.ChangedByUserId);
        Assert.NotEqual(default, change.ChangedAt);
    }

    [Fact]
    public async Task Add_RefusesAnEmailAlreadyUsed_RegardlessOfCase_EvenByADeactivatedAccount()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var existing = await AddUserAsync(RoleCodes.Supply, isActive: false);

        await using var context = CreateContext();
        var exception = await Assert.ThrowsAsync<AccountEmailAlreadyUsedException>(
            () => new AccountService(context).AddAsync(existing.Email.ToUpperInvariant(), "Other", RoleCodes.Finance, admin.Id));
        Assert.Equal(existing.Email, exception.Email);
    }

    [Fact]
    public async Task Add_RefusesAnUnknownRole()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);

        await using var context = CreateContext();
        await Assert.ThrowsAsync<RoleNotFoundException>(
            () => new AccountService(context).AddAsync(NewEmail(), "Jean", "Volunteer", admin.Id));
    }

    [Theory]
    [InlineData("", "Jean")]
    [InlineData("no-at-sign.org", "Jean")]
    [InlineData("jean@moissonmontreal.org", "   ")]
    public async Task Add_RefusesAnInvalidEmailOrName(string email, string name)
    {
        var admin = await AddUserAsync(RoleCodes.Admin);

        await using var context = CreateContext();
        await Assert.ThrowsAsync<ArgumentException>(
            () => new AccountService(context).AddAsync(email, name, RoleCodes.Supply, admin.Id));
    }

    [Fact]
    public async Task Add_RefusesATooLongEmail()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var email = new string('a', User.EmailMaxLength) + "@moissonmontreal.org";

        await using var context = CreateContext();
        await Assert.ThrowsAsync<ArgumentException>(
            () => new AccountService(context).AddAsync(email, "Jean", RoleCodes.Supply, admin.Id));
    }

    [Fact]
    public async Task Update_EditsTheNameAndEmail_AndLogsOnlyWhatChanged()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await new AccountService(context).UpdateAsync(user.Id, user.Email, "New Name", user.Version, admin.Id);
        }

        var saved = await ReloadAsync(user.Id);
        Assert.Equal((user.Email, "New Name"), (saved.Email, saved.DisplayName));
        Assert.NotNull(saved.UpdatedAt);
        var change = Assert.Single(await HistoryAsync(user.Id));
        Assert.Equal(UserAccountAction.Updated, change.Action);
        Assert.Equal((user.DisplayName, "New Name"), (change.OldDisplayName, change.NewDisplayName));
        Assert.Equal((null, null), (change.OldEmail, change.NewEmail));
    }

    [Fact]
    public async Task Update_WithTheSameValues_SavesNothing()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await new AccountService(context).UpdateAsync(user.Id, $" {user.Email.ToUpperInvariant()} ", user.DisplayName, user.Version, admin.Id);
        }

        Assert.Equal(user.Version, (await ReloadAsync(user.Id)).Version);
        Assert.Empty(await HistoryAsync(user.Id));
    }

    [Fact]
    public async Task Update_RefusesAnOldVersion_AndKeepsTheOtherChange()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var first = CreateContext())
        {
            await new AccountService(first).UpdateAsync(user.Id, user.Email, "First Admin's Edit", user.Version, admin.Id);
        }

        await using (var second = CreateContext())
        {
            await Assert.ThrowsAsync<AccountEditedMeanwhileException>(
                () => new AccountService(second).UpdateAsync(user.Id, user.Email, "Second Admin's Edit", user.Version, admin.Id));
        }

        Assert.Equal("First Admin's Edit", (await ReloadAsync(user.Id)).DisplayName);
        Assert.Single(await HistoryAsync(user.Id));
    }

    [Fact]
    public async Task Update_RefusesAChangeSavedByAnotherAdminBetweenTheCheckAndTheSave()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext(new SameAccountEditedMeanwhile(postgres.ConnectionString, user.Id)))
        {
            await Assert.ThrowsAsync<AccountEditedMeanwhileException>(
                () => new AccountService(context).UpdateAsync(user.Id, user.Email, "Overwrite", user.Version, admin.Id));
        }

        Assert.Equal("Edited meanwhile", (await ReloadAsync(user.Id)).DisplayName);
        Assert.Empty(await HistoryAsync(user.Id));
    }

    [Fact]
    public async Task Update_RefusesAnEmailUsedByAnotherAccount()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);
        var other = await AddUserAsync(RoleCodes.Finance);

        await using var context = CreateContext();
        await Assert.ThrowsAsync<AccountEmailAlreadyUsedException>(
            () => new AccountService(context).UpdateAsync(user.Id, other.Email, user.DisplayName, user.Version, admin.Id));
    }

    [Fact]
    public async Task Update_RefusesAnAccountThatDoesNotExist()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);

        await using var context = CreateContext();
        var exception = await Assert.ThrowsAsync<AccountNotFoundException>(
            () => new AccountService(context).UpdateAsync(-1, NewEmail(), "Jean", 1, admin.Id));
        Assert.Equal(-1, exception.UserId);
    }

    [Fact]
    public async Task ChangeRole_ChangesTheRole_AndLogsTheOldAndNewRoles()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            var changed = await new AccountService(context).ChangeRoleAsync(user.Id, RoleCodes.Finance, user.Version, admin.Id);
            Assert.Equal(RoleCodes.Finance, changed.Role.Code);
        }

        var change = Assert.Single(await HistoryAsync(user.Id));
        Assert.Equal(UserAccountAction.RoleChanged, change.Action);
        Assert.Equal((await RoleIdAsync(RoleCodes.Supply), await RoleIdAsync(RoleCodes.Finance)), (change.OldRoleId, change.NewRoleId));
    }

    [Fact]
    public async Task ChangeRole_RefusesTheAdministratorsOwnAccount()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);

        await using var context = CreateContext();
        await Assert.ThrowsAsync<OwnAccountChangeException>(
            () => new AccountService(context).ChangeRoleAsync(admin.Id, RoleCodes.Supply, admin.Version, admin.Id));
    }

    [Fact]
    public async Task ChangeRole_RefusesToRemoveTheRoleOfTheLastActiveAdmin()
    {
        await DeactivateAllAdminsAsync();
        var lastAdmin = await AddUserAsync(RoleCodes.Admin);
        var actor = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await Assert.ThrowsAsync<LastActiveAdminException>(
                () => new AccountService(context).ChangeRoleAsync(lastAdmin.Id, RoleCodes.Supply, lastAdmin.Version, actor.Id));
        }

        Assert.Equal(RoleCodes.Admin, (await ReloadAsync(lastAdmin.Id)).Role.Code);
    }

    [Fact]
    public async Task Deactivate_RefusesTheAdministratorsOwnAccount()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);

        await using var context = CreateContext();
        await Assert.ThrowsAsync<OwnAccountChangeException>(
            () => new AccountService(context).DeactivateAsync(admin.Id, admin.Version, admin.Id));
    }

    [Fact]
    public async Task Deactivate_RefusesTheLastActiveAdmin()
    {
        await DeactivateAllAdminsAsync();
        var lastAdmin = await AddUserAsync(RoleCodes.Admin);
        var actor = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await Assert.ThrowsAsync<LastActiveAdminException>(
                () => new AccountService(context).DeactivateAsync(lastAdmin.Id, lastAdmin.Version, actor.Id));
        }

        Assert.True((await ReloadAsync(lastAdmin.Id)).IsActive);
    }

    [Fact]
    public async Task Deactivate_IsAllowedForAnAdmin_WhenAnotherActiveAdminRemains()
    {
        await DeactivateAllAdminsAsync();
        var admin = await AddUserAsync(RoleCodes.Admin);
        var otherAdmin = await AddUserAsync(RoleCodes.Admin);

        await using (var context = CreateContext())
        {
            await new AccountService(context).DeactivateAsync(otherAdmin.Id, otherAdmin.Version, admin.Id);
        }

        Assert.False((await ReloadAsync(otherAdmin.Id)).IsActive);
    }

    [Fact]
    public async Task TwoAdminsDeactivatingEachOtherAtTheSameTime_LeaveOneActiveAdmin()
    {
        await DeactivateAllAdminsAsync();
        var alice = await AddUserAsync(RoleCodes.Admin);
        var bob = await AddUserAsync(RoleCodes.Admin);

        await using var aliceContext = CreateContext();
        await using var bobContext = CreateContext();
        var results = await Task.WhenAll(
            TryAsync(() => new AccountService(aliceContext).DeactivateAsync(bob.Id, bob.Version, alice.Id)),
            TryAsync(() => new AccountService(bobContext).DeactivateAsync(alice.Id, alice.Version, bob.Id)));

        Assert.Single(results, error => error is null);
        Assert.Single(results, error => error is LastActiveAdminException);
        Assert.Equal(1, await CountActiveAdminsAsync());
    }

    [Fact]
    public async Task DeactivateThenReactivate_KeepsTheAccount_AndLogsBothChanges()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await new AccountService(context).DeactivateAsync(user.Id, user.Version, admin.Id);
        }

        var deactivated = await ReloadAsync(user.Id);
        Assert.False(deactivated.IsActive);

        await using (var context = CreateContext())
        {
            await new AccountService(context).ReactivateAsync(user.Id, deactivated.Version, admin.Id);
        }

        var reactivated = await ReloadAsync(user.Id);
        Assert.True(reactivated.IsActive);
        Assert.Equal((user.Email, user.DisplayName, user.RoleId), (reactivated.Email, reactivated.DisplayName, reactivated.RoleId));
        var history = await HistoryAsync(user.Id);
        Assert.Equal([UserAccountAction.Deactivated, UserAccountAction.Reactivated], history.Select(c => c.Action));
        Assert.Equal([(true, false), (false, true)], history.Select(c => (c.OldIsActive!.Value, c.NewIsActive!.Value)));
    }

    [Fact]
    public async Task Deactivate_AnInactiveAccount_SavesNothing()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply, isActive: false);

        await using (var context = CreateContext())
        {
            await new AccountService(context).DeactivateAsync(user.Id, user.Version, admin.Id);
        }

        Assert.Empty(await HistoryAsync(user.Id));
    }

    [Fact]
    public async Task ChangeRole_ToTheSameRole_SavesNothing()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await new AccountService(context).ChangeRoleAsync(user.Id, RoleCodes.Supply, user.Version, admin.Id);
        }

        Assert.Equal(user.Version, (await ReloadAsync(user.Id)).Version);
        Assert.Empty(await HistoryAsync(user.Id));
    }

    [Fact]
    public async Task Reactivate_AnActiveAccount_SavesNothing()
    {
        var admin = await AddUserAsync(RoleCodes.Admin);
        var user = await AddUserAsync(RoleCodes.Supply);

        await using (var context = CreateContext())
        {
            await new AccountService(context).ReactivateAsync(user.Id, user.Version, admin.Id);
        }

        Assert.Equal(user.Version, (await ReloadAsync(user.Id)).Version);
        Assert.Empty(await HistoryAsync(user.Id));
    }

    private static string NewEmail() => $"employee.{Guid.NewGuid():N}@moissonmontreal.org";

    private static async Task<Exception?> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task<User> AddUserAsync(string roleCode, bool isActive = true)
    {
        await using var context = CreateContext();
        var user = new User
        {
            Email = NewEmail(),
            DisplayName = "Test " + roleCode,
            RoleId = await context.Roles.Where(r => r.Code == roleCode).Select(r => r.Id).SingleAsync(),
            IsActive = isActive,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return await ReloadAsync(user.Id);
    }

    private async Task DeactivateAllAdminsAsync()
    {
        await using var context = CreateContext();
        await context.Users
            .Where(u => u.Role.Code == RoleCodes.Admin)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
    }

    private async Task<int> CountActiveAdminsAsync()
    {
        await using var context = CreateContext();
        return await context.Users.CountAsync(u => u.IsActive && u.Role.Code == RoleCodes.Admin);
    }

    private async Task<int> RoleIdAsync(string roleCode)
    {
        await using var context = CreateContext();
        return await context.Roles.Where(r => r.Code == roleCode).Select(r => r.Id).SingleAsync();
    }

    private async Task<User> ReloadAsync(int userId)
    {
        await using var context = CreateContext();
        return await context.Users.Include(u => u.Role).AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private async Task<User> FindByEmailAsync(string email)
    {
        await using var context = CreateContext();
        return await context.Users.AsNoTracking().SingleAsync(u => u.Email == email);
    }

    private async Task<List<UserAccountChange>> HistoryAsync(int userId)
    {
        await using var context = CreateContext();
        return await context.UserAccountChanges.Where(c => c.UserId == userId).OrderBy(c => c.ChangedAt).ToListAsync();
    }

    private MoissonDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).AddInterceptors(interceptors).Options);

    // Edits the account from another connection just before the tested save, as another administrator would.
    private sealed class SameAccountEditedMeanwhile(string connectionString, int userId) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = new MoissonDbContext(
                    new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(connectionString).Options);
                await other.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(set => set.SetProperty(u => u.DisplayName, "Edited meanwhile"), cancellationToken);
            }

            return result;
        }
    }
}
