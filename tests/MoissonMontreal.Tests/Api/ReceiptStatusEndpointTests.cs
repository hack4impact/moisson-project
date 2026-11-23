using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Controllers;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class ReceiptStatusEndpointTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>, IAsyncLifetime
{
    private static readonly Employee Supply = new(RoleCodes.Supply, "Sam Supply", "aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Employee Director = new(RoleCodes.SupplyDirector, "Dana Director", "aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Employee Finance = new(RoleCodes.Finance, "Fred Finance", "aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Employee Admin = new(RoleCodes.Admin, "Ada Admin", "aaaaaaaa-0000-0000-0000-000000000004");

    public async Task InitializeAsync()
    {
        await using var context = factory.CreateDbContext();
        if (await context.Users.AnyAsync(u => u.Email == Supply.Email))
        {
            return;
        }

        foreach (var employee in new[] { Supply, Director, Finance, Admin })
        {
            context.Users.Add(new User
            {
                Email = employee.Email,
                DisplayName = employee.Name,
                EntraObjectId = Guid.Parse(employee.ObjectId),
                RoleId = await context.Roles.Where(r => r.Code == employee.Role).Select(r => r.Id).SingleAsync(),
            });
        }

        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Workflow_RecordsEveryTransitionWithTheEmployeeAndTheDate()
    {
        var receiptId = await CreateReceiptAsync();
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Supply, receiptId, "submit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Director, receiptId, "approve-level-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Finance, receiptId, "approve-level-2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(Supply, receiptId, "issue")).StatusCode);

        await using var context = factory.CreateDbContext();
        var changes = await context.ReceiptStatusChanges
            .Where(c => c.ReceiptId == receiptId)
            .OrderBy(c => c.ChangedAt)
            .Select(c => new { c.FromStatus, c.ToStatus, c.ChangedBy.DisplayName, c.ChangedAt })
            .ToListAsync();
        Assert.Equal(
            [
                (ReceiptStatus.Draft, ReceiptStatus.PendingSupplyApproval, Supply.Name),
                (ReceiptStatus.PendingSupplyApproval, ReceiptStatus.PendingFinanceApproval, Director.Name),
                (ReceiptStatus.PendingFinanceApproval, ReceiptStatus.Approved, Finance.Name),
                (ReceiptStatus.Approved, ReceiptStatus.Issued, Supply.Name),
            ],
            changes.Select(c => (c.FromStatus, c.ToStatus, c.DisplayName)));
        Assert.All(changes, c => Assert.InRange(c.ChangedAt, start.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(ReceiptStatus.Issued, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task Submit_ReturnsTheNewStatusAndTheNextActions()
    {
        var receiptId = await CreateReceiptAsync();

        var response = await PostAsync(Supply, receiptId, "submit");

        var change = await response.Content.ReadFromJsonAsync<ReceiptStatusChangeDto>();
        Assert.NotNull(change);
        Assert.Equal(receiptId, change.ReceiptId);
        Assert.Equal(ReceiptStatus.Draft, change.FromStatus);
        Assert.Equal(ReceiptStatus.PendingSupplyApproval, change.ToStatus);
        Assert.Equal(Supply.Name, change.ChangedBy);
        Assert.Null(change.Reason);
        Assert.Equal([ReceiptAction.ApproveLevel1, ReceiptAction.RefuseLevel1, ReceiptAction.Cancel], change.AllowedActions);
    }

    [Fact]
    public async Task Submit_SendsTheStatusAsText()
    {
        var receiptId = await CreateReceiptAsync();

        var response = await PostAsync(Supply, receiptId, "submit");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PendingSupplyApproval", json.RootElement.GetProperty("toStatus").GetString());
    }

    [Fact]
    public async Task InvalidTransition_ReturnsConflictWithAClearMessage()
    {
        var receiptId = await CreateReceiptAsync();

        var response = await PostAsync(Supply, receiptId, "issue");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Status change not allowed", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "Issue is not allowed on a receipt in status Draft. Allowed actions: Submit, Cancel.",
            json.RootElement.GetProperty("detail").GetString());
        Assert.Equal("Draft", json.RootElement.GetProperty("currentStatus").GetString());
        Assert.Equal(["Submit", "Cancel"], json.RootElement.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(ReceiptStatus.Draft, await StatusOfAsync(receiptId));
        Assert.Equal(0, await ChangeCountAsync(receiptId));
    }

    [Fact]
    public async Task CancelledReceipt_RefusesEveryAction()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "cancel", new ReceiptReasonRequest("Duplicate"));

        var response = await PostAsync(Supply, receiptId, "submit");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ReceiptStatus.Cancelled, await StatusOfAsync(receiptId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refuse_ReturnsBadRequest_WhenTheReasonIsMissing(string? reason)
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "submit");

        var response = await PostAsync(Director, receiptId, "refuse-level-1", new ReceiptReasonRequest(reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(["A reason is required for RefuseLevel1."], problem.Errors["Reason"]);
        Assert.Equal(ReceiptStatus.PendingSupplyApproval, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task Refuse_ReturnsBadRequest_WhenTheReasonIsTooLong()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "submit");

        var response = await PostAsync(
            Director, receiptId, "refuse-level-1", new ReceiptReasonRequest(new string('x', ReceiptStatusChange.ReasonMaxLength + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refuse_SendsTheReceiptBackToDraftWithTheReason()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "submit");
        await PostAsync(Director, receiptId, "approve-level-1");

        var response = await PostAsync(Finance, receiptId, "refuse-level-2", new ReceiptReasonRequest("  Wrong quantity of bread  "));

        var change = await response.Content.ReadFromJsonAsync<ReceiptStatusChangeDto>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReceiptStatus.Draft, change?.ToStatus);
        Assert.Equal("Wrong quantity of bread", change?.Reason);
        Assert.Equal(ReceiptStatus.Draft, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenTheReasonIsMissing()
    {
        var receiptId = await CreateReceiptAsync();

        var response = await PostAsync(Supply, receiptId, "cancel", new ReceiptReasonRequest(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ReceiptStatus.Draft, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task Cancel_CancelsAnIssuedReceipt()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "submit");
        await PostAsync(Director, receiptId, "approve-level-1");
        await PostAsync(Finance, receiptId, "approve-level-2");
        await PostAsync(Supply, receiptId, "issue");

        var response = await PostAsync(Supply, receiptId, "cancel", new ReceiptReasonRequest("Wrong donor"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReceiptStatus.Cancelled, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task Approve_ReturnsForbidden_WhenTheApproverSubmittedTheReceipt()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Admin, receiptId, "submit");

        var response = await PostAsync(Admin, receiptId, "approve-level-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Cannot approve your own receipt", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(ReceiptStatus.PendingSupplyApproval, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task ApproveLevel2_ReturnsForbidden_WhenTheApproverSubmittedTheReceipt()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Admin, receiptId, "submit");
        await PostAsync(Director, receiptId, "approve-level-1");

        var response = await PostAsync(Admin, receiptId, "approve-level-2");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ReceiptStatus.PendingFinanceApproval, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task Approve_ReturnsForbidden_WhenTheRoleIsNotAllowed()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "submit");

        var response = await PostAsync(Finance, receiptId, "approve-level-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ReceiptStatus.PendingSupplyApproval, await StatusOfAsync(receiptId));
    }

    [Fact]
    public async Task UnknownReceipt_ReturnsNotFound()
    {
        var response = await PostAsync(Supply, Guid.NewGuid(), "submit");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SimultaneousApprovals_LetOnlyOneSucceed()
    {
        var receiptId = await CreateReceiptAsync();
        await PostAsync(Supply, receiptId, "submit");

        var responses = await Task.WhenAll(
            PostAsync(Director, receiptId, "approve-level-1"),
            PostAsync(Admin, receiptId, "approve-level-1"));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, await ChangeCountAsync(receiptId));
    }

    [Fact]
    public void Endpoints_RequireThePermissionOfTheirTransition()
    {
        var endpoints = typeof(ReceiptStatusController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToDictionary(m => Enum.Parse<ReceiptAction>(m.Name), m => m.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        Assert.Equivalent(Enum.GetValues<ReceiptAction>(), endpoints.Keys, strict: true);
        Assert.All(ReceiptStateMachine.Transitions, t => Assert.Equal(t.Permission, endpoints[t.Action]));
    }

    private async Task<HttpResponseMessage> PostAsync(Employee employee, Guid receiptId, string action, ReceiptReasonRequest? body = null)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.CreateToken(email: employee.Email, objectId: employee.ObjectId));
        var url = $"/api/receipts/{receiptId}/{action}";
        return body is null ? await client.PostAsync(url, null) : await client.PostAsJsonAsync(url, body);
    }

    private async Task<Guid> CreateReceiptAsync()
    {
        await using var context = factory.CreateDbContext();
        var receipt = new Receipt { Donor = new Donor { Name = "Boulangerie Saint-Laurent" }, TotalAmount = 44.97m };
        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();
        return receipt.Id;
    }

    private async Task<ReceiptStatus> StatusOfAsync(Guid receiptId)
    {
        await using var context = factory.CreateDbContext();
        return await context.Receipts.Where(r => r.Id == receiptId).Select(r => r.Status).SingleAsync();
    }

    private async Task<int> ChangeCountAsync(Guid receiptId)
    {
        await using var context = factory.CreateDbContext();
        return await context.ReceiptStatusChanges.CountAsync(c => c.ReceiptId == receiptId);
    }

    private sealed record Employee(string Role, string Name, string ObjectId)
    {
        public string Email => $"{Role.ToLowerInvariant()}@moissonmontreal.org";
    }
}
