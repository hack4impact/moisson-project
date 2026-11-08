using System.Text;
using System.Text.RegularExpressions;

namespace MoissonMontreal.Tests.Docs;

/// <summary>
/// Reads the subset of DBML used in docs/database into a <see cref="DatabaseSchema"/>.
/// Anything it does not understand throws, so a diagram cannot silently skip part of the check.
/// </summary>
internal static partial class DbmlReader
{
    public static DatabaseSchema Read(string dbml)
    {
        var lines = dbml.Replace("\r\n", "\n").Split('\n').Select(RemoveComment).ToList();
        var tables = new Dictionary<string, SchemaTable>();
        var foreignKeys = new Dictionary<string, SchemaForeignKey>();
        var records = new Dictionary<string, SchemaRecords>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            Match match;
            if ((match = TableStart().Match(line)).Success)
            {
                var body = TakeBlock(lines, ref i);
                Add(tables, match.Groups[1].Value, ReadTable(match.Groups[1].Value, body), "Table");
            }
            else if ((match = RefLine().Match(line)).Success)
            {
                var settings = Settings(match.Groups[6].Value);
                var onDelete = Unquote(settings.GetValueOrDefault("delete") ?? "no action");
                Add(foreignKeys, match.Groups[1].Value, new SchemaForeignKey(
                    match.Groups[2].Value,
                    Columns(match.Groups[3].Value),
                    match.Groups[4].Value,
                    Columns(match.Groups[5].Value),
                    onDelete.ToLowerInvariant()), "Ref");
            }
            else if ((match = RecordsStart().Match(line)).Success)
            {
                var body = TakeBlock(lines, ref i);
                var rows = body.Where(l => l.Trim().Length > 0).Select(l => (IReadOnlyList<string>)SplitTopLevel(l.Trim()).Select(Unquote).ToList()).ToList();
                Add(records, match.Groups[1].Value, new SchemaRecords(Columns($"({match.Groups[2].Value})"), rows), "Records");
            }
            else if (SkippedBlockStart().IsMatch(line))
            {
                TakeBlock(lines, ref i);
            }
            else
            {
                throw new FormatException($"Unsupported DBML on line {i + 1}: {line}");
            }
        }

        return new DatabaseSchema(tables, foreignKeys, records);
    }

    private static SchemaTable ReadTable(string table, List<string> body)
    {
        var columns = new Dictionary<string, SchemaColumn>();
        var indexes = new Dictionary<string, SchemaIndex>();
        var checks = new Dictionary<string, string>();

        for (var i = 0; i < body.Count; i++)
        {
            var line = body[i].Trim();
            if (line.Length == 0 || line.StartsWith("Note:", StringComparison.Ordinal))
            {
                continue;
            }

            if (line == "indexes {")
            {
                foreach (var indexLine in TakeBlock(body, ref i).Select(l => l.Trim()).Where(l => l.Length > 0))
                {
                    var match = IndexLine().Match(indexLine);
                    if (!match.Success)
                    {
                        throw new FormatException($"Unsupported index in {table}: {indexLine}");
                    }

                    var settings = Settings(match.Groups[2].Value);
                    var name = settings.GetValueOrDefault("name") is { } quoted
                        ? Unquote(quoted)
                        : throw new FormatException($"Index without a name in {table}: {indexLine}");
                    var primary = settings.ContainsKey("pk");
                    Add(indexes, name, new SchemaIndex(Columns(match.Groups[1].Value), primary, primary || settings.ContainsKey("unique")), "Index");
                }
            }
            else if (line == "checks {")
            {
                foreach (var checkLine in TakeBlock(body, ref i).Select(l => l.Trim()).Where(l => l.Length > 0))
                {
                    var match = CheckLine().Match(checkLine);
                    var name = match.Success ? Settings(match.Groups[2].Value).GetValueOrDefault("name") : null;
                    if (name is null || !checks.TryAdd(Unquote(name), match.Groups[1].Value))
                    {
                        throw new FormatException($"Check without a unique name in {table}: {checkLine}");
                    }
                }
            }
            else
            {
                var match = ColumnLine().Match(line);
                if (!match.Success)
                {
                    throw new FormatException($"Unsupported column in {table}: {line}");
                }

                var settings = Settings(match.Groups[3].Value);
                var unknown = settings.Keys.Except(["not null", "null", "increment", "default", "note"]).ToList();
                if (unknown.Count > 0)
                {
                    throw new FormatException($"Unsupported column setting in {table}: {string.Join(", ", unknown)}");
                }

                Add(columns, match.Groups[1].Value, new SchemaColumn(
                    SchemaText.NormalizeType(match.Groups[2].Value),
                    settings.ContainsKey("not null"),
                    SchemaText.NormalizeDefault(settings.GetValueOrDefault("default")),
                    settings.ContainsKey("increment")), "Column");
            }
        }

        return new SchemaTable(columns, indexes, checks);
    }

    private static List<string> TakeBlock(List<string> lines, ref int start)
    {
        var depth = 0;
        var body = new List<string>();
        for (var i = start; i < lines.Count; i++)
        {
            depth += Count(lines[i], '{') - Count(lines[i], '}');
            if (i > start && depth == 0)
            {
                start = i;
                return body;
            }

            if (i > start)
            {
                body.Add(lines[i]);
            }
        }

        throw new FormatException($"Block starting on line {start + 1} is never closed");
    }

    private static Dictionary<string, string?> Settings(string text)
    {
        var settings = new Dictionary<string, string?>();
        foreach (var part in SplitTopLevel(text).Where(p => p.Length > 0))
        {
            var colon = part.IndexOf(':');
            if (colon < 0)
            {
                settings[part.ToLowerInvariant()] = null;
            }
            else
            {
                settings[part[..colon].Trim().ToLowerInvariant()] = part[(colon + 1)..].Trim();
            }
        }

        return settings;
    }

    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        char? quote = null;

        foreach (var c in text)
        {
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
            }
            else if (c is '\'' or '`' or '"')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        parts.Add(current.ToString().Trim());
        return parts;
    }

    private static string RemoveComment(string line)
    {
        char? quote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
            }
            else if (c is '\'' or '`' or '"')
            {
                quote = c;
            }
            else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static IReadOnlyList<string> Columns(string text) =>
        text.Trim().TrimStart('(').TrimEnd(')').Split(',').Select(c => c.Trim()).ToList();

    private static string Unquote(string value)
    {
        var text = value.Trim();
        return text.Length >= 2 && text[0] == text[^1] && text[0] is '\'' or '"' ? text[1..^1] : text;
    }

    private static int Count(string line, char c) => line.Count(x => x == c);

    private static void Add<T>(Dictionary<string, T> items, string name, T item, string kind)
    {
        if (!items.TryAdd(name, item))
        {
            throw new FormatException($"{kind} {name} is defined twice");
        }
    }

    [GeneratedRegex(@"^Table\s+(\w+)\s*\{$")]
    private static partial Regex TableStart();

    [GeneratedRegex(@"^Ref\s+(\w+)\s*:\s*(\w+)\.(\(.*?\)|\w+)\s*>\s*(\w+)\.(\(.*?\)|\w+)\s*(?:\[(.*)\])?$")]
    private static partial Regex RefLine();

    [GeneratedRegex(@"^Records\s+(\w+)\((.*)\)\s*\{$")]
    private static partial Regex RecordsStart();

    [GeneratedRegex(@"^(Project|TableGroup)\s+\w+\s*\{$")]
    private static partial Regex SkippedBlockStart();

    [GeneratedRegex(@"^(\w+)\s+(\w+(?:\(\s*\d+\s*(?:,\s*\d+\s*)?\))?)\s*(?:\[(.*)\])?$")]
    private static partial Regex ColumnLine();

    [GeneratedRegex(@"^(\(.*?\)|\w+)\s*\[(.*)\]$")]
    private static partial Regex IndexLine();

    [GeneratedRegex(@"^`(.*)`\s*\[(.*)\]$")]
    private static partial Regex CheckLine();
}
