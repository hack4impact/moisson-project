namespace MoissonMontreal.Tests.Docs;

/// <summary>
/// Locates the repository folders from the test output folder, by walking up to the folder that holds the solution file.
/// </summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string DatabaseDocs => Path.Combine(Root, "docs", "database");

    public static string Migrations => Path.Combine(Root, "src", "MoissonMontreal.Api", "Data", "Migrations");

    public static IEnumerable<string> DbmlFiles => Directory.EnumerateFiles(DatabaseDocs, "*.dbml").Order();

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "MoissonMontreal.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new DirectoryNotFoundException("MoissonMontreal.slnx was not found above the test output folder");
    }
}
