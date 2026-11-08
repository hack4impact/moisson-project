namespace MoissonMontreal.Tests.Docs;

public class SchemaDriftTests
{
    private static readonly string FullSchema = File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, "schema.dbml")).ReplaceLineEndings("\n");

    public static TheoryData<string, string, string, string> Drifts => new()
    {
        { "lower price precision", "unit_price numeric(12,4)", "unit_price numeric(12,2)", "receipt_items.unit_price: type" },
        { "longer text", "quality varchar(1)", "quality varchar(2)", "food_items.quality: type" },
        { "missing column", "  updated_at timestamptz [null, note: 'Stored in UTC, empty if never changed']\n", "", "Column users.updated_at is missing from the diagram" },
        { "extra column", "  display_name varchar(200) [not null]\n", "  display_name varchar(200) [not null]\n  nickname varchar(50) [null]\n", "Column users.nickname is in the diagram but not in the database" },
        { "wrong nullability", "entra_object_id uuid [null", "entra_object_id uuid [not null", "users.entra_object_id: not null" },
        { "wrong default", "default: true", "default: false", "users.is_active: default" },
        { "wrong time zone default", "'America/Montreal'", "'UTC'", "receipts.receipt_date: default" },
        { "missing default", "created_at timestamptz [not null, default: `now()`, note: 'Stored in UTC']\n\n  indexes {\n    id [pk, name: 'pk_donors']", "created_at timestamptz [not null, note: 'Stored in UTC']\n\n  indexes {\n    id [pk, name: 'pk_donors']", "donors.created_at: default" },
        { "missing increment", "id integer [not null, increment]", "id integer [not null]", "users.id: increment" },
        { "renamed index", "name: 'ix_receipt_items_food_item_id',", "name: 'ix_receipt_items_food_item',", "Index ix_receipt_items_food_item_id is missing from the diagram" },
        { "duplicates allowed", "food_item_id [unique, name: 'ix_receipt_items_food_item_id'", "food_item_id [name: 'ix_receipt_items_food_item_id'", "Index ix_receipt_items_food_item_id: unique" },
        { "index columns in another order", "(receipt_id, food_item_id) [pk", "(food_item_id, receipt_id) [pk", "Index pk_receipt_items: columns" },
        { "missing check", "    `quality IN ('A', 'B', 'C')` [name: 'ck_food_items_quality']\n", "", "Check ck_food_items_quality is missing from the diagram" },
        { "wrong check expression", "`quantity > 0` [name: 'ck_food_items_quantity_positive']", "`quantity >= 0` [name: 'ck_food_items_quantity_positive']", "Check ck_food_items_quantity_positive: expression" },
        { "wrong delete rule", "receipts.(id, donor_id) [delete: cascade]", "receipts.(id, donor_id) [delete: restrict]", "Ref fk_receipt_items_receipts_receipt_id_donor_id: delete rule" },
        { "donor left out of a link", "receipt_items.(food_item_id, donor_id) > food_items.(id, donor_id)", "receipt_items.food_item_id > food_items.id", "Ref fk_receipt_items_food_items_food_item_id_donor_id: columns" },
        { "missing link", "Ref fk_users_roles_role_id: users.role_id > roles.id [delete: restrict]\n", "", "Ref fk_users_roles_role_id is missing from the diagram" },
        { "renamed table", "Table donors {", "Table donor {", "Table donors is missing from the diagram" }
    };

    [Fact]
    public void FullSchemaDiagram_MatchesItself()
    {
        var schema = DbmlReader.Read(FullSchema);

        Assert.Empty(DatabaseSchema.Compare(schema, schema, diagramMustHaveEveryTable: true));
    }

    [Theory]
    [MemberData(nameof(Drifts))]
    public void Compare_ReportsTheDifference_WhenTheDiagramDrifts(string drift, string find, string replace, string expected)
    {
        Assert.Contains(find, FullSchema);
        var database = DbmlReader.Read(FullSchema);
        var diagram = DbmlReader.Read(FullSchema.Replace(find, replace));

        var differences = DatabaseSchema.Compare(diagram, database, diagramMustHaveEveryTable: true);

        Assert.True(differences.Any(d => d.StartsWith(expected, StringComparison.Ordinal)), $"{drift}: expected \"{expected}\" in:\n{string.Join("\n", differences)}");
    }

    [Fact]
    public void Compare_ReportsALinkToATableThatIsNotInTheDiagram()
    {
        var database = DbmlReader.Read(FullSchema);
        var diagram = DbmlReader.Read("""
            Table users {
              id integer [not null]
              role_id integer [not null]
            }

            Ref fk_users_roles_role_id: users.role_id > roles.id [delete: restrict]
            """);

        var differences = DatabaseSchema.Compare(diagram, database, diagramMustHaveEveryTable: false);

        Assert.Contains("Ref fk_users_roles_role_id links a table that is not in the diagram", differences);
    }

    [Theory]
    [InlineData("Ref: users.role_id < roles.id")]
    [InlineData("Enum quality { A B C }")]
    [InlineData("Table users {\n  id integer [pk]\n}")]
    [InlineData("Table users {\n  id integer\n  indexes {\n    id [pk]\n  }\n}")]
    [InlineData("Table users {\n  id integer\n  checks {\n    `id > 0`\n  }\n}")]
    [InlineData("Table users {\n  id integer\n  id integer\n}")]
    [InlineData("Table users {\n  id integer\n")]
    public void Read_Throws_OnDbmlItCannotCheck(string dbml)
    {
        Assert.Throws<FormatException>(() => DbmlReader.Read(dbml));
    }

    [Fact]
    public void Read_KeepsCommasAndColonsInsideNotes()
    {
        var schema = DbmlReader.Read("""
            Table food_items {
              quality varchar(1) [not null, note: 'A, B or C: the grade']
            }
            """);

        var column = schema.Tables["food_items"].Columns["quality"];
        Assert.Equal(new SchemaColumn("character varying(1)", true, null, false), column);
    }

    [Theory]
    [InlineData("varchar(256)", "character varying(256)")]
    [InlineData("character varying(256)", "character varying(256)")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("timestamp with time zone", "timestamp with time zone")]
    [InlineData("numeric(12, 4)", "numeric(12,4)")]
    [InlineData("int", "integer")]
    public void NormalizeType_GivesTheSameSpellingForTheSameType(string type, string expected)
    {
        Assert.Equal(expected, SchemaText.NormalizeType(type));
    }

    [Fact]
    public void Read_GivesTheSameSchema_WithWindowsLineEndings()
    {
        var unix = DbmlReader.Read(FullSchema);
        var windows = DbmlReader.Read(FullSchema.Replace("\n", "\r\n"));

        Assert.Empty(DatabaseSchema.Compare(windows, unix, diagramMustHaveEveryTable: true));
        Assert.Equal(unix.Tables.Keys, windows.Tables.Keys);
    }

    [Fact]
    public void Read_KeepsTheDefaultAndCheckExactlyAsWritten()
    {
        var schema = DbmlReader.Read("""
            Table receipts {
              receipt_date date [not null, default: `(now() AT TIME ZONE 'America/Montreal')::date`]
              total_amount numeric(12,2) [not null]

              checks {
                `(total_amount + 1) * 2 >= 0` [name: 'ck_total']
              }
            }
            """);

        Assert.Equal("(now() AT TIME ZONE 'America/Montreal')::date", schema.Tables["receipts"].Columns["receipt_date"].Default);
        Assert.Equal("(total_amount + 1) * 2 >= 0", schema.Tables["receipts"].Checks["ck_total"]);
    }
}
