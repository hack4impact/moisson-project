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
        var checks = new Dictionary<string, Dictionary<string, string>>();
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
                r.IsDBNull(4) ? null : r.GetString(4),
                r.GetBoolean(5));
        });

        // pg_get_indexdef returns the column name, or the full expression of an expression column, so none is skipped.
        await ForEachRowAsync(connection, """
            SELECT t.relname, i.relname, ix.indisprimary, ix.indisunique,
                   array(SELECT pg_get_indexdef(ix.indexrelid, n, true) FROM generate_series(1, ix.indnkeyatts) n ORDER BY n),
                   pg_get_expr(ix.indpred, ix.indrelid, true)
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            WHERE t.relnamespace = 'public'::regnamespace
            """, r =>
        {
            Table(indexes, r.GetString(0))[r.GetString(1)] = new SchemaIndex(
                r.GetFieldValue<string[]>(4),
                r.GetBoolean(2),
                r.GetBoolean(3),
                r.IsDBNull(5) ? null : r.GetString(5));
        });

        await ForEachRowAsync(connection, """
            SELECT c.conrelid::regclass::text, c.conname, c.contype::text, c.confrelid::regclass::text, c.confdeltype::text,
                   array(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(num, ord)
                         JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.num ORDER BY k.ord),
                   array(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(num, ord)
                         JOIN pg_attribute a ON a.attrelid = c.confrelid AND a.attnum = k.num ORDER BY k.ord),
                   pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            WHERE c.connamespace = 'public'::regnamespace AND c.contype IN ('c', 'f')
            """, r =>
        {
            var table = r.GetString(0).Trim('"');
            if (r.GetString(2) == "c")
            {
                Table(checks, table)[r.GetString(1)] = r.GetString(7);
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
                    checks.GetValueOrDefault(t) ?? new Dictionary<string, string>()));

        return new DatabaseSchema(tables, foreignKeys, new Dictionary<string, SchemaRecords>());
    }

    /// <summary>
    /// Has PostgreSQL rewrite the defaults and checks of the diagram in its own spelling, by adding them to empty copies of the real tables
    /// and reading them back. The two sides can then be compared exactly. Nothing is kept: the transaction is rolled back.
    /// An expression PostgreSQL rejects is replaced by a message, so the comparison reports it.
    /// </summary>
    public static async Task<DatabaseSchema> CanonicalizeAsync(NpgsqlConnection connection, DatabaseSchema diagram)
    {
        var existing = (await ReadAsync(connection)).Tables;
        var tables = new Dictionary<string, SchemaTable>();

        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var (name, table) in diagram.Tables)
        {
            if (!existing.TryGetValue(name, out var real))
            {
                tables[name] = table;
                continue;
            }

            var probe = $"probe_{name}";
            await ExecuteAsync(connection, $"CREATE TEMP TABLE \"{probe}\" (LIKE public.\"{name}\")");

            var columns = new Dictionary<string, SchemaColumn>();
            foreach (var (columnName, column) in table.Columns)
            {
                columns[columnName] = column.Default is null || !real.Columns.ContainsKey(columnName)
                    ? column
                    : column with
                    {
                        Default = await RewriteAsync(connection, $"ALTER TABLE \"{probe}\" ALTER COLUMN \"{columnName}\" SET DEFAULT {column.Default}", $"""
                            SELECT pg_get_expr(d.adbin, d.adrelid) FROM pg_attrdef d
                            JOIN pg_attribute a ON a.attrelid = d.adrelid AND a.attnum = d.adnum
                            WHERE d.adrelid = '"{probe}"'::regclass AND a.attname = '{columnName}'
                            """)
                    };
            }

            var checks = new Dictionary<string, string>();
            foreach (var (checkName, expression) in table.Checks)
            {
                checks[checkName] = await RewriteAsync(connection, $"ALTER TABLE \"{probe}\" ADD CONSTRAINT \"{checkName}\" CHECK ({expression})", $"""
                    SELECT pg_get_constraintdef(oid) FROM pg_constraint
                    WHERE conrelid = '"{probe}"'::regclass AND conname = '{checkName}'
                    """);
            }

            tables[name] = table with { Columns = columns, Checks = checks };
        }

        await transaction.RollbackAsync();
        return diagram with { Tables = tables };
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

    private static async Task<string> RewriteAsync(NpgsqlConnection connection, string change, string read)
    {
        await ExecuteAsync(connection, "SAVEPOINT rewrite");
        try
        {
            await ExecuteAsync(connection, change);
            await using var command = new NpgsqlCommand(read, connection);
            var rewritten = (string)(await command.ExecuteScalarAsync())!;
            await ExecuteAsync(connection, "RELEASE SAVEPOINT rewrite");
            return rewritten;
        }
        catch (PostgresException exception)
        {
            await ExecuteAsync(connection, "ROLLBACK TO SAVEPOINT rewrite");
            return $"invalid ({exception.MessageText})";
        }
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

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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
