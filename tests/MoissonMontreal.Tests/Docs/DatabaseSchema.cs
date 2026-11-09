namespace MoissonMontreal.Tests.Docs;

/// <summary>
/// Structure of a database, read either from a .dbml diagram or from a migrated PostgreSQL database, so the two can be compared.
/// </summary>
internal sealed record DatabaseSchema(
    IReadOnlyDictionary<string, SchemaTable> Tables,
    IReadOnlyDictionary<string, SchemaForeignKey> ForeignKeys,
    IReadOnlyDictionary<string, SchemaRecords> Records)
{
    /// <summary>
    /// Lists every difference between the diagram and the database. An empty list means they match.
    /// </summary>
    public static List<string> Compare(DatabaseSchema diagram, DatabaseSchema database, bool diagramMustHaveEveryTable)
    {
        var differences = new List<string>();

        foreach (var name in diagram.Tables.Keys.Except(database.Tables.Keys))
        {
            differences.Add($"Table {name} is in the diagram but not in the database");
        }

        if (diagramMustHaveEveryTable)
        {
            foreach (var name in database.Tables.Keys.Except(diagram.Tables.Keys))
            {
                differences.Add($"Table {name} is missing from the diagram");
            }
        }

        foreach (var (name, table) in diagram.Tables)
        {
            if (database.Tables.TryGetValue(name, out var actual))
            {
                CompareTable(name, table, actual, differences);
            }
        }

        foreach (var (name, key) in diagram.ForeignKeys)
        {
            if (!diagram.Tables.ContainsKey(key.Table) || !diagram.Tables.ContainsKey(key.ReferencedTable))
            {
                differences.Add($"Ref {name} links a table that is not in the diagram");
            }
        }

        var diagramKeys = diagram.ForeignKeys.Where(k => diagram.Tables.ContainsKey(k.Value.Table)).ToDictionary();
        var databaseKeys = database.ForeignKeys.Where(k => diagram.Tables.ContainsKey(k.Value.Table)).ToDictionary();
        CompareNamed("Ref ", diagramKeys, databaseKeys, differences, (name, expected, actual) =>
        {
            Check(differences, $"Ref {name}: table", expected.Table, actual.Table);
            Check(differences, $"Ref {name}: columns", Join(expected.Columns), Join(actual.Columns));
            Check(differences, $"Ref {name}: referenced table", expected.ReferencedTable, actual.ReferencedTable);
            Check(differences, $"Ref {name}: referenced columns", Join(expected.ReferencedColumns), Join(actual.ReferencedColumns));
            Check(differences, $"Ref {name}: delete rule", expected.OnDelete, actual.OnDelete);
        });

        return differences;
    }

    private static void CompareTable(string table, SchemaTable diagram, SchemaTable database, List<string> differences)
    {
        CompareNamed($"Column {table}.", diagram.Columns, database.Columns, differences, (name, expected, actual) =>
        {
            Check(differences, $"{table}.{name}: type", expected.Type, actual.Type);
            Check(differences, $"{table}.{name}: not null", expected.NotNull, actual.NotNull);
            Check(differences, $"{table}.{name}: default", expected.Default, actual.Default);
            Check(differences, $"{table}.{name}: increment", expected.Increment, actual.Increment);
        });

        CompareNamed("Index ", diagram.Indexes, database.Indexes, differences, (name, expected, actual) =>
        {
            Check(differences, $"Index {name}: columns", Join(expected.Columns), Join(actual.Columns));
            Check(differences, $"Index {name}: primary key", expected.Primary, actual.Primary);
            Check(differences, $"Index {name}: unique", expected.Unique, actual.Unique);
            Check(differences, $"Index {name}: condition", expected.Where ?? "none", actual.Where ?? "none");
        });

        CompareNamed("Check ", diagram.Checks, database.Checks, differences, (name, expected, actual) =>
        {
            Check(differences, $"Check {name}: expression", expected, actual);
        });
    }

    private static void CompareNamed<T>(
        string label,
        IReadOnlyDictionary<string, T> diagram,
        IReadOnlyDictionary<string, T> database,
        List<string> differences,
        Action<string, T, T> compare)
    {
        foreach (var name in diagram.Keys.Except(database.Keys))
        {
            differences.Add($"{label}{name} is in the diagram but not in the database");
        }

        foreach (var name in database.Keys.Except(diagram.Keys))
        {
            differences.Add($"{label}{name} is missing from the diagram");
        }

        foreach (var name in diagram.Keys.Intersect(database.Keys))
        {
            compare(name, diagram[name], database[name]);
        }
    }

    private static void Check<T>(List<string> differences, string what, T diagram, T database)
    {
        if (!EqualityComparer<T>.Default.Equals(diagram, database))
        {
            differences.Add($"{what} is {diagram} in the diagram but {database} in the database");
        }
    }

    private static string Join(IEnumerable<string> values) => $"({string.Join(", ", values)})";
}

/// <summary>
/// One table: its columns, its indexes (including the primary key and unique constraints) and its checks, by name.
/// </summary>
internal sealed record SchemaTable(
    IReadOnlyDictionary<string, SchemaColumn> Columns,
    IReadOnlyDictionary<string, SchemaIndex> Indexes,
    IReadOnlyDictionary<string, string> Checks);

/// <summary>
/// One column. The type is normalized so the diagram and PostgreSQL spellings compare equal.
/// The default is the expression as written, until PostgreSQL rewrites it in its own spelling.
/// </summary>
internal sealed record SchemaColumn(string Type, bool NotNull, string? Default, bool Increment);

/// <summary>
/// One index, primary key or unique constraint, with its columns or expressions in order,
/// and the condition of a partial index. A diagram cannot show a condition, so it is always null there.
/// </summary>
internal sealed record SchemaIndex(IReadOnlyList<string> Columns, bool Primary, bool Unique, string? Where = null);

/// <summary>
/// One foreign key, with its delete rule in lowercase words such as restrict or cascade.
/// </summary>
internal sealed record SchemaForeignKey(
    string Table,
    IReadOnlyList<string> Columns,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns,
    string OnDelete);

/// <summary>
/// Rows that the diagram says a table always contains, such as the four roles.
/// </summary>
internal sealed record SchemaRecords(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
