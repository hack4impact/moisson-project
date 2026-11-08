using System.Globalization;
using Npgsql;

namespace MoissonMontreal.Tests.Docs;

/// <summary>
/// Reads the real structure of the public schema of a PostgreSQL database into a <see cref="DatabaseSchema"/>.
/// The Entity Framework history table is left out, since it is not part of the app's data.
/// </summary>
internal static class PostgresSchemaReader
{
    private const string HistoryTable = "__EFMigrationsHistory";

    public static async Task<DatabaseSchema> ReadAsync(NpgsqlConnection connection)
    {
        var columns = new Dictionary<string, Dictionary<string, SchemaColumn>>();
        var indexes = new Dictionary<string, Dictionary<string, SchemaIndex>>();
        var checks = new Dictionary<string, HashSet<string>>();
        var foreignKeys = new Dictionary<string, SchemaForeignKey>();

        await ForEachRowAsync(connection, """
            SELECT c.relname, a.attname, format_type(a.atttypid, a.atttypmod), a.attnotnull,
                   pg_get_expr(d.adbin, d.adrelid), a.attidentity <> ''
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
            """, r =>
        {
            Table(columns, r.GetString(0))[r.GetString(1)] = new SchemaColumn(
                SchemaText.NormalizeType(r.GetString(2)),
                r.GetBoolean(3),
                SchemaText.NormalizeDefault(r.IsDBNull(4) ? null : r.GetString(4)),
                r.GetBoolean(5));
        });

        await ForEachRowAsync(connection, """
            SELECT t.relname, i.relname, ix.indisprimary, ix.indisunique,
                   array(SELECT a.attname::text FROM unnest(ix.indkey) WITH ORDINALITY k(num, ord)
                         JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.num ORDER BY k.ord)
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            WHERE t.relnamespace = 'public'::regnamespace
            """, r =>
        {
            Table(indexes, r.GetString(0))[r.GetString(1)] = new SchemaIndex(r.GetFieldValue<string[]>(4), r.GetBoolean(2), r.GetBoolean(3));
        });

        await ForEachRowAsync(connection, """
            SELECT c.conrelid::regclass::text, c.conname, c.contype::text, c.confrelid::regclass::text, c.confdeltype::text,
                   array(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(num, ord)
                         JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.num ORDER BY k.ord),
                   array(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(num, ord)
                         JOIN pg_attribute a ON a.attrelid = c.confrelid AND a.attnum = k.num ORDER BY k.ord)
            FROM pg_constraint c
            WHERE c.connamespace = 'public'::regnamespace AND c.contype IN ('c', 'f')
            """, r =>
        {
            var table = r.GetString(0).Trim('"');
            if (r.GetString(2) == "c")
            {
                Table(checks, table).Add(r.GetString(1));
                return;
            }

            foreignKeys[r.GetString(1)] = new SchemaForeignKey(
                table,
                r.GetFieldValue<string[]>(5),
                r.GetString(3).Trim('"'),
                r.GetFieldValue<string[]>(6),
                DeleteRule(r.GetString(4)));
        });

        var tables = columns.Keys
            .Where(t => t != HistoryTable)
            .ToDictionary(
                t => t,
                t => new SchemaTable(
                    columns[t],
                    indexes.GetValueOrDefault(t) ?? new Dictionary<string, SchemaIndex>(),
                    checks.GetValueOrDefault(t) ?? new HashSet<string>()));

        return new DatabaseSchema(tables, foreignKeys, new Dictionary<string, SchemaRecords>());
    }

    public static async Task<List<IReadOnlyList<string>>> ReadRowsAsync(NpgsqlConnection connection, string table, IReadOnlyList<string> columns)
    {
        var rows = new List<IReadOnlyList<string>>();
        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        await ForEachRowAsync(connection, $"SELECT {columnList} FROM \"{table}\" ORDER BY 1", r =>
        {
            rows.Add(Enumerable.Range(0, r.FieldCount)
                .Select(i => Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "")
                .ToList());
        });

        return rows;
    }

    private static string DeleteRule(string code) => code switch
    {
        "a" => "no action",
        "r" => "restrict",
        "c" => "cascade",
        "n" => "set null",
        "d" => "set default",
        _ => throw new InvalidOperationException($"Unknown delete rule {code}")
    };

    private static TValue Table<TValue>(Dictionary<string, TValue> items, string table) where TValue : new()
    {
        if (!items.TryGetValue(table, out var value))
        {
            value = new TValue();
            items[table] = value;
        }

        return value;
    }

    private static async Task ForEachRowAsync(NpgsqlConnection connection, string sql, Action<NpgsqlDataReader> read)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            read(reader);
        }
    }
}
