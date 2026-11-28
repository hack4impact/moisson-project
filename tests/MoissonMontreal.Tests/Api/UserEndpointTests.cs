using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Tests.Fixtures;

namespace MoissonMontreal.Tests.Api;

[Trait("Category", "Integration")]
public class UserEndpointTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task List_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        await factory.SignInAsAsync(RoleCodes.Admin);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users", Request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    public async Task List_ReturnsForbidden_WhenRoleIsNotAdmin(string role)
    {
        using var client = await CreateClientAsync(role);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleCodes.Supply)]
    [InlineData(RoleCodes.SupplyDirector)]
    [InlineData(RoleCodes.Finance)]
    public async Task Create_ReturnsForbidden_AndCreatesNothing_WhenRoleIsNotAdmin(string role)
    {
        using var client = await CreateClientAsync(role);

        var response = await client.PostAsJsonAsync("/api/users", Request());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task Create_ReturnsTheAccount_WithTheAdminWhoCreatedIt()
    {
        using var client = await CreateClientAsync();
        var adminId = await AdminIdAsync();
        var before = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync("/api/users", new UserRequest(" Marie Tremblay ", " Marie.Tremblay@MoissonMontreal.org ", RoleCodes.Finance));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.True(user!.Id > 0);
        Assert.Equal("Marie Tremblay", user.DisplayName);
        Assert.Equal("marie.tremblay@moissonmontreal.org", user.Email);
        Assert.Equal(RoleCodes.Finance, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(adminId, user.CreatedById);
        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
        Assert.InRange(user.CreatedAt, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));

        await using var context = CreateContext();
        var saved = await context.Users.Include(u => u.Role).SingleAsync(u => u.Id == user.Id);
        Assert.Equal("marie.tremblay@moissonmontreal.org", saved.Email);
        Assert.Equal(RoleCodes.Finance, saved.Role.Code);
        Assert.Equal(adminId, saved.CreatedById);
        Assert.Null(saved.EntraObjectId);
        Assert.Null(saved.UpdatedAt);
    }

    [Fact]
    public async Task Create_LogsTheNewAccountAndItsAuthor_WithoutTheEmail()
    {
        var logs = new CapturedLogs();
        using var loggingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        await factory.SignInAsAsync(RoleCodes.Admin);
        using var client = loggingFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        var adminId = await AdminIdAsync();

        var user = await CreateAsync(client, Request(email: "log.check@moissonmontreal.org"));

        Assert.Contains($"Account {user.Id} created by account {adminId}", logs.Messages);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("log.check", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("luc.gagnon@moissonmontreal.org")]
    [InlineData("LUC.GAGNON@MOISSONMONTREAL.ORG")]
    [InlineData(" Luc.Gagnon@moissonmontreal.org ")]
    public async Task Create_ReturnsConflict_WhenTheEmailIsAlreadyUsed_WhateverItsCapitalLetters(string email)
    {
        using var client = await CreateClientAsync();
        await CreateAsync(client, Request(email: "luc.gagnon@moissonmontreal.org"));

        var response = await client.PostAsJsonAsync("/api/users", Request(name: "Autre Luc", email: email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Email already used", problem!.Title);
        Assert.Equal(2, await CountUsersAsync());
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheEmailIsTheAdminsOwn()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", Request(email: AuthenticatedApiFactory.Email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WithOneMessagePerField_AndCreatesNothing()
    {
        using var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new UserRequest(" ", "not-an-email", "Manager"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["DisplayName is required."], problem!.Errors[nameof(UserRequest.DisplayName)]);
        Assert.Equal(["Email must be a valid email address."], problem.Errors[nameof(UserRequest.Email)]);
        Assert.Equal(["Role must be one of: Supply, SupplyDirector, Finance, Admin."], problem.Errors[nameof(UserRequest.Role)]);
        Assert.Equal(3, problem.Errors.Count);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task List_ReturnsEveryAccount_ActiveOrNot_OrderedByName()
    {
        using var client = await CreateClientAsync();
        var adminId = await AdminIdAsync();
        var created = await CreateAsync(client, Request(name: "Bruno Lefebvre", email: "bruno@moissonmontreal.org", role: RoleCodes.Supply));
        await using (var context = CreateContext())
        {
            var supplyDirector = await context.Roles.SingleAsync(r => r.Code == RoleCodes.SupplyDirector);
            context.Users.Add(new User { DisplayName = "Anne Roy", Email = "anne@moissonmontreal.org", Role = supplyDirector, IsActive = false });
            await context.SaveChangesAsync();
        }

        var users = await client.GetFromJsonAsync<List<UserDto>>("/api/users");

        Assert.Equal(["Anne Roy", "Bruno Lefebvre", AuthenticatedApiFactory.Name], users!.Select(u => u.DisplayName));
        Assert.False(users[0].IsActive);
        Assert.Equal(RoleCodes.SupplyDirector, users[0].Role);
        Assert.Null(users[0].CreatedById);
        Assert.Equal(created, users[1]);
        Assert.Equal(adminId, users[1].CreatedById);
        Assert.Equal(RoleCodes.Admin, users[2].Role);
    }

    [Fact]
    public async Task Responses_HaveOnlyTheAccountFields_AndNoPassword()
    {
        using var client = await CreateClientAsync();
        string[] expected = ["createdAt", "createdById", "displayName", "email", "id", "isActive", "role"];

        var created = await client.PostAsJsonAsync("/api/users", Request());
        var list = await client.GetAsync("/api/users");

        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(expected, createdJson.RootElement.EnumerateObject().Select(p => p.Name).Order());
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.All(listJson.RootElement.EnumerateArray(), user =>
            Assert.Equal(expected, user.EnumerateObject().Select(p => p.Name).Order()));
    }

    private async Task<HttpClient> CreateClientAsync(string role = RoleCodes.Admin)
    {
        await factory.SignInAsAsync(role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static UserRequest Request(
        string? name = "Marie Tremblay",
        string? email = "marie.tremblay@moissonmontreal.org",
        string? role = RoleCodes.Supply) =>
        new(name, email, role);

    private static async Task<UserDto> CreateAsync(HttpClient client, UserRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    private async Task<int> AdminIdAsync()
    {
        await using var context = CreateContext();
        return await context.Users.Where(u => u.Email == AuthenticatedApiFactory.Email).Select(u => u.Id).SingleAsync();
    }

    private async Task<int> CountUsersAsync()
    {
        await using var context = CreateContext();
        return await context.Users.CountAsync();
    }

    private MoissonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(factory.ConnectionString).Options);

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new Logger(_messages);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));
        }
    }
}
