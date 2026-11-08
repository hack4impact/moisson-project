using System.Text.RegularExpressions;

namespace MoissonMontreal.Tests.Docs;

public partial class DatabaseDocsTests
{
    public static TheoryData<string> MarkdownFiles =>
        [.. Directory.EnumerateFiles(RepositoryPaths.DatabaseDocs, "*.md").Order().Select(f => Path.GetFileName(f))];

    public static TheoryData<string> DocsFiles =>
        [.. Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "docs"), "*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".md" or ".dbml" or ".sql")
            .Order()
            .Select(f => Path.GetRelativePath(RepositoryPaths.Root, f))];

    [Theory]
    [MemberData(nameof(MarkdownFiles))]
    public void EveryLinkAndImage_PointsToAFileThatExists(string fileName)
    {
        var markdown = File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, fileName));

        var missing = MarkdownLink().Matches(markdown)
            .Select(m => m.Groups[1].Value)
            .Where(target => !target.StartsWith("http", StringComparison.Ordinal) && !target.StartsWith('#'))
            .Select(target => target.Split('#')[0])
            .Where(target => !Path.Exists(Path.Combine(RepositoryPaths.DatabaseDocs, target)))
            .ToList();

        Assert.True(missing.Count == 0, $"{fileName} links to missing files: {string.Join(", ", missing)}");
    }

    [Fact]
    public void SchemaPage_DescribesEveryTableOfTheDiagram()
    {
        var schema = DbmlReader.Read(File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, "schema.dbml")));
        var page = File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, "schema.md"));

        Assert.All(schema.Tables.Keys, table => Assert.Contains($"\n### {table}\n", page));
    }

    [Theory]
    [MemberData(nameof(DocsFiles))]
    public void DocsFile_HasNoEmDash(string relativePath)
    {
        Assert.DoesNotContain('—', File.ReadAllText(Path.Combine(RepositoryPaths.Root, relativePath)));
    }

    [Fact]
    public void SqlScripts_StartWithoutByteOrderMark()
    {
        var scripts = Directory.EnumerateFiles(Path.Combine(RepositoryPaths.DatabaseDocs, "sql"), "*.sql").ToList();

        Assert.NotEmpty(scripts);
        Assert.All(scripts, script => Assert.False(File.ReadAllBytes(script).AsSpan().StartsWith((byte[])[0xEF, 0xBB, 0xBF]), $"{Path.GetFileName(script)} starts with a BOM"));
    }

    [Fact]
    public void SqlScripts_CoverEveryMigrationInOrder()
    {
        var migrations = Directory.EnumerateFiles(RepositoryPaths.Migrations, "*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => MigrationId().IsMatch(name!))
            .Order()
            .ToList();
        var scripts = Directory.EnumerateFiles(Path.Combine(RepositoryPaths.DatabaseDocs, "sql"), "*.sql").Order().ToList();

        Assert.Equal(migrations.Count, scripts.Count);
        for (var i = 0; i < scripts.Count; i++)
        {
            Assert.StartsWith($"{i + 1:0000}_", Path.GetFileName(scripts[i]));
            Assert.Contains($"VALUES ('{migrations[i]}'", File.ReadAllText(scripts[i]));
        }
    }

    [Theory]
    [MemberData(nameof(MarkdownFiles))]
    [MemberData(nameof(DbmlFileNames))]
    public void MigrationNamesInTheDocs_Exist(string fileName)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryPaths.DatabaseDocs, fileName));

        var missing = MigrationReference().Matches(text)
            .Select(m => m.Value)
            .Distinct()
            .Where(id => !File.Exists(Path.Combine(RepositoryPaths.Migrations, $"{id}.cs")))
            .ToList();

        Assert.True(missing.Count == 0, $"{fileName} names migrations that do not exist: {string.Join(", ", missing)}");
    }

    public static TheoryData<string> DbmlFileNames => [.. RepositoryPaths.DbmlFiles.Select(f => Path.GetFileName(f))];

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"^\d{14}_\w+$")]
    private static partial Regex MigrationId();

    [GeneratedRegex(@"\b\d{14}_[A-Za-z]\w*")]
    private static partial Regex MigrationReference();
}
