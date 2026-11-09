using Microsoft.EntityFrameworkCore;
using MoissonMontreal.Api.Data;
using MoissonMontreal.Tests.Fixtures;
using Npgsql;

namespace MoissonMontreal.Tests.Docs;

[Trait("Category", "Integration")]
public class SchemaDiagramTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string FullSchema = File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, "schema.dbml")).ReplaceLineEndings("\n");

    public static TheoryData<string> DbmlFiles => [.. RepositoryPaths.DbmlFiles.Select(f => Path.GetFileName(f))];

    public static TheoryData<string, string, string> ExpressionDrifts => new()
    {
        { "`quality IN ('A', 'B', 'C')`", "`quality IN ('A', 'B')`", "Check ck_food_items_quality: expression" },
        { "`quantity > 0` [name: 'ck_food_items_quantity_positive']", "`quantity >= 0` [name: 'ck_food_items_quantity_positive']", "Check ck_food_items_quantity_positive: expression" },
        { "`quality_percent BETWEEN 0 AND 100`", "`quality_percent BETWEEN 0 AND 75`", "Check ck_receipt_items_quality_percent_range: expression" },
        { "`quality_percent BETWEEN 0 AND 100`", "`quality_percent BETWEEN 0 AND 100 OR unknown_column > 0`", "Check ck_receipt_items_quality_percent_range: expression is invalid" },
        { "'America/Montreal'", "'America/Toronto'", "receipts.receipt_date: default" },
        { "default: true", "default: false", "users.is_active: default" }
    };

    public static TheoryData<string, string> IndexesADiagramCannotShow => new()
    {
        {
            "DROP INDEX ix_receipt_items_food_item_id; CREATE UNIQUE INDEX ix_receipt_items_food_item_id ON receipt_items (food_item_id) WHERE quantity > 0",
            "Index ix_receipt_items_food_item_id: condition is none in the diagram but quantity > 0::numeric in the database"
        },
        {
            "DROP INDEX ix_users_email; CREATE UNIQUE INDEX ix_users_email ON users (lower(email::text))",
            "Index ix_users_email: columns is (email) in the diagram but (lower(email::text)) in the database"
        }
    };

    [Theory]
    [MemberData(nameof(DbmlFiles))]
    public async Task Diagram_MatchesTheMigratedDatabase(string fileName)
    {
        var diagram = await ReadDiagramAsync(File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, fileName)));
        var database = await ReadDatabaseAsync();

        var differences = DatabaseSchema.Compare(diagram, database, diagramMustHaveEveryTable: fileName == "schema.dbml");

        Assert.True(differences.Count == 0, $"{fileName} does not match the migrations:\n{string.Join("\n", differences)}");
    }

    [Fact]
    public async Task FullSchemaDiagram_HasEveryTableOfTheDatabase()
    {
        var diagram = DbmlReader.Read(FullSchema);
        var database = await ReadDatabaseAsync();

        Assert.Equal(database.Tables.Keys.Order(), diagram.Tables.Keys.Order());
        Assert.Equal(database.ForeignKeys.Keys.Order(), diagram.ForeignKeys.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(ExpressionDrifts))]
    public async Task Diagram_ReportsADifference_WhenAnExpressionDiffers(string find, string replace, string expected)
    {
        Assert.Contains(find, FullSchema);
        var diagram = await ReadDiagramAsync(FullSchema.Replace(find, replace));
        var database = await ReadDatabaseAsync();

        var differences = DatabaseSchema.Compare(diagram, database, diagramMustHaveEveryTable: true);

        Assert.True(differences.Any(d => d.StartsWith(expected, StringComparison.Ordinal)), $"Expected \"{expected}\" in:\n{string.Join("\n", differences)}");
    }

    [Fact]
    public async Task Diagram_KeepsParenthesesThatChangeTheMeaning()
    {
        var grouped = await ReadDiagramAsync(FullSchema.Replace("`total_amount >= 0`", "`(total_amount + 1) * 2 >= 0`"));
        var ungrouped = await ReadDiagramAsync(FullSchema.Replace("`total_amount >= 0`", "`total_amount + 1 * 2 >= 0`"));

        var differences = DatabaseSchema.Compare(grouped, ungrouped, diagramMustHaveEveryTable: true);

        Assert.Single(differences, d => d.StartsWith("Check ck_receipts_total_amount_not_negative: expression", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(IndexesADiagramCannotShow))]
    public async Task Diagram_ReportsAPartialOrExpressionIndex(string change, string expected)
    {
        var diagram = await ReadDiagramAsync(FullSchema);
        await MigrateAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(change, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var database = await PostgresSchemaReader.ReadAsync(connection);
        await transaction.RollbackAsync();

        Assert.Contains(expected, DatabaseSchema.Compare(diagram, database, diagramMustHaveEveryTable: true));
    }

    [Theory]
    [MemberData(nameof(DbmlFiles))]
    public async Task DiagramRecords_MatchTheRowsCreatedByTheMigrations(string fileName)
    {
        var diagram = DbmlReader.Read(File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, fileName)));
        await MigrateAsync();

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
        var diagram = DbmlReader.Read(FullSchema);

        var roles = Assert.Single(diagram.Records, r => r.Key == "roles").Value;
        Assert.Equal(["Supply", "SupplyDirector", "Finance", "Admin"], roles.Rows.Select(r => r[1]));
    }

    private async Task MigrateAsync()
    {
        await using var context = new MoissonDbContext(new DbContextOptionsBuilder<MoissonDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await context.Database.MigrateAsync();
    }

    private async Task<DatabaseSchema> ReadDatabaseAsync()
    {
        await MigrateAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        return await PostgresSchemaReader.ReadAsync(connection);
    }

    private async Task<DatabaseSchema> ReadDiagramAsync(string dbml)
    {
        await MigrateAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        return await PostgresSchemaReader.CanonicalizeAsync(connection, DbmlReader.Read(dbml));
    }
}
