using System.Text.RegularExpressions;

namespace MoissonMontreal.Tests.Docs;

/// <summary>
/// Normalizes type names, so that the spelling used in a diagram and the one returned by PostgreSQL compare equal.
/// Expressions (defaults and checks) are not rewritten here: PostgreSQL rewrites them itself, see PostgresSchemaReader.CanonicalizeAsync.
/// </summary>
internal static partial class SchemaText
{
    public static string NormalizeType(string type)
    {
        var value = WhiteSpace().Replace(type.Trim().ToLowerInvariant(), "");
        value = Varchar().Replace(value, "character varying$1");

        return value switch
        {
            "timestamptz" => "timestamp with time zone",
            "timestampwithtimezone" => "timestamp with time zone",
            "int" or "int4" => "integer",
            "bool" => "boolean",
            _ => value.StartsWith("charactervarying", StringComparison.Ordinal)
                ? "character varying" + value["charactervarying".Length..]
                : value
        };
    }

    public static string? NormalizeDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('`');

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"^varchar(\(\d+\))?$")]
    private static partial Regex Varchar();
}
