using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class ReceiptNumberingEndpointTests(PermissionsApiFactory factory) : IClassFixture<PermissionsApiFactory>, IAsyncLifetime
{
    private const string SupplyEmail = "supply.numbering@moissonmontreal.org";
    private const string SupplyObjectId = "bbbbbbbb-0000-0000-0000-000000000001";

    public async Task InitializeAsync()
    {
        await using var context = factory.CreateDbContext();
        if (await context.Users.AnyAsync(u => u.Email == SupplyEmail))
        {
            return;
        }

        context.Users.Add(new User
        {
            Email = SupplyEmail,
            DisplayName = "Sam Supply",
            EntraObjectId = Guid.Parse(SupplyObjectId),
            RoleId = await context.Roles.Where(r => r.Code == RoleCodes.Supply).Select(r => r.Id).SingleAsync(),
        });
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Issue_GivesTheNextNumber()
    {
        var receiptId = await CreateReceiptAsync(ReceiptStatus.Approved);
        var before = await LastNumberAsync();

        var response = await PostAsync(receiptId, "issue");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = ReceiptNumber.Format(before + 1);
        var change = await response.Content.ReadFromJsonAsync<ReceiptStatusChangeDto>();
        Assert.Equal(expected, change?.ReceiptNumber);
        Assert.Equal(expected, await NumberOfAsync(receiptId));
        Assert.Equal(before + 1, await LastNumberAsync());
    }

    [Fact]
    public async Task Issue_GivesConsecutiveNumbers()
    {
        var first = await CreateReceiptAsync(ReceiptStatus.Approved);
        var second = await CreateReceiptAsync(ReceiptStatus.Approved);
        var before = await LastNumberAsync();

        await PostAsync(first, "issue");
        await PostAsync(second, "issue");

        Assert.Equal(ReceiptNumber.Format(before + 1), await NumberOfAsync(first));
        Assert.Equal(ReceiptNumber.Format(before + 2), await NumberOfAsync(second));
    }

    [Fact]
    public async Task OtherActions_DoNotGiveANumber()
    {
        var receiptId = await CreateReceiptAsync(ReceiptStatus.Draft);
        var before = await LastNumberAsync();

        var response = await PostAsync(receiptId, "submit");

        var change = await response.Content.ReadFromJsonAsync<ReceiptStatusChangeDto>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(change?.ReceiptNumber);
        Assert.Null(await NumberOfAsync(receiptId));
        Assert.Equal(before, await LastNumberAsync());
    }

    [Fact]
    public async Task RefusedIssue_DoesNotUseANumber()
    {
        var receiptId = await CreateReceiptAsync(ReceiptStatus.Draft);
        var before = await LastNumberAsync();

        var response = await PostAsync(receiptId, "issue");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(await NumberOfAsync(receiptId));
        Assert.Equal(before, await LastNumberAsync());
    }

    [Fact]
    public async Task CancelledReceipt_KeepsItsNumber_AndTheNumberIsNeverGivenAgain()
    {
        var cancelled = await CreateReceiptAsync(ReceiptStatus.Approved);
        var next = await CreateReceiptAsync(ReceiptStatus.Approved);
        await PostAsync(cancelled, "issue");
        var number = await NumberOfAsync(cancelled);

        var response = await PostAsync(cancelled, "cancel", new ReceiptReasonRequest("Wrong donor"));
        await PostAsync(next, "issue");

        var change = await response.Content.ReadFromJsonAsync<ReceiptStatusChangeDto>();
        Assert.Equal(number, change?.ReceiptNumber);
        Assert.Equal(number, await NumberOfAsync(cancelled));
        Assert.NotEqual(number, await NumberOfAsync(next));
    }

    [Fact]
    public async Task SimultaneousIssues_GetDifferentConsecutiveNumbers()
    {
        var receiptIds = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            receiptIds.Add(await CreateReceiptAsync(ReceiptStatus.Approved));
        }

        var before = await LastNumberAsync();

        var responses = await Task.WhenAll(receiptIds.Select(id => PostAsync(id, "issue")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var numbers = new List<string?>();
        foreach (var id in receiptIds)
        {
            numbers.Add(await NumberOfAsync(id));
        }

        Assert.Equal(
            Enumerable.Range(before + 1, 20).Select(ReceiptNumber.Format).Order(),
            numbers.Order());
        Assert.Equal(before + 20, await LastNumberAsync());
    }

    [Fact]
    public async Task SimultaneousIssuesOfTheSameReceipt_UseOneNumber()
    {
        var receiptId = await CreateReceiptAsync(ReceiptStatus.Approved);
        var before = await LastNumberAsync();

        var responses = await Task.WhenAll(PostAsync(receiptId, "issue"), PostAsync(receiptId, "issue"));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(ReceiptNumber.Format(before + 1), await NumberOfAsync(receiptId));
        Assert.Equal(before + 1, await LastNumberAsync());
    }

    private async Task<HttpResponseMessage> PostAsync(Guid receiptId, string action, ReceiptReasonRequest? body = null)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.CreateToken(email: SupplyEmail, objectId: SupplyObjectId));
        var url = $"/api/receipts/{receiptId}/{action}";
        return body is null ? await client.PostAsync(url, null) : await client.PostAsJsonAsync(url, body);
    }

    private async Task<Guid> CreateReceiptAsync(ReceiptStatus status)
    {
        await using var context = factory.CreateDbContext();
        var receipt = new Receipt
        {
            Donor = new Donor { Name = "Boulangerie Saint-Laurent" },
            TotalAmount = 44.97m,
            Status = status,
        };
        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();
        return receipt.Id;
    }

    private async Task<string?> NumberOfAsync(Guid receiptId)
    {
        await using var context = factory.CreateDbContext();
        return await context.Receipts.Where(r => r.Id == receiptId).Select(r => r.ReceiptNumber).SingleAsync();
    }

    private async Task<int> LastNumberAsync()
    {
        await using var context = factory.CreateDbContext();
        return await context.ReceiptNumberCounters
            .Where(c => c.Series == ReceiptNumber.Series)
            .Select(c => c.LastNumber)
            .SingleAsync();
    }
}
