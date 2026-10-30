using Testcontainers.PostgreSql;

namespace MoissonMontreal.Tests.Fixtures;

/// <summary>
/// Starts a single PostgreSQL container shared by every test of a class.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}