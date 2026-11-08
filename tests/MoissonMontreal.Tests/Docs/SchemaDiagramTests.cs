using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

namespace MoissonMontreal.Tests.Docs;

[Trait("Category", "Integration")]
public class SchemaDiagramTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    public static TheoryData<string> DbmlFiles => [.. RepositoryPaths.DbmlFiles.Select(f => Path.GetFileName(f))];

    [Theory]
    [MemberData(nameof(DbmlFiles))]
    public async Task Diagram_MatchesTheMigratedDatabase(string fileName)
    {
        var diagram = ReadDiagram(fileName);
        var database = await ReadDatabaseAsync();

        var differences = DatabaseSchema.Compare(diagram, database, diagramMustHaveEveryTable: fileName == "schema.dbml");

        Assert.True(differences.Count == 0, $"{fileName} does not match the migrations:\n{string.Join("\n", differences)}");
    }

    [Fact]
    public async Task FullSchemaDiagram_HasEveryTableOfTheDatabase()
    {
        var diagram = ReadDiagram("schema.dbml");
        var database = await ReadDatabaseAsync();

        Assert.Equal(database.Tables.Keys.Order(), diagram.Tables.Keys.Order());
        Assert.Equal(database.ForeignKeys.Keys.Order(), diagram.ForeignKeys.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(DbmlFiles))]
    public async Task DiagramRecords_MatchTheRowsCreatedByTheMigrations(string fileName)
    {
        var diagram = ReadDiagram(fileName);
        await ReadDatabaseAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        foreach (var (table, records) in diagram.Records)
        {
            var rows = await PostgresSchemaReader.ReadRowsAsync(connection, table, records.Columns);

            Assert.Equal(records.Rows, rows);
        }
    }

    [Fact]
    public void FullSchemaDiagram_ListsTheFourRoles()
    {
        var diagram = ReadDiagram("schema.dbml");

        var roles = Assert.Single(diagram.Records, r => r.Key == "roles").Value;
        Assert.Equal(["Supply", "SupplyDirector", "Finance", "Admin"], roles.Rows.Select(r => r[1]));
    }

    private static DatabaseSchema ReadDiagram(string fileName) =>
        DbmlReader.Read(File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, fileName)));

    private async Task<DatabaseSchema> ReadDatabaseAsync()
    {
        await using (var context = new MoissonDbContext(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options))
        {
            await context.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        return await PostgresSchemaReader.ReadAsync(connection);
    }
}
