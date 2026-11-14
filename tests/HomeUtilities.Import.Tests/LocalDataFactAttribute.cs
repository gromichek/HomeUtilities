namespace HomeUtilities.Import.Tests;

/// <summary>
/// A test that needs the owner's real files and is skipped everywhere else (CI, cloud sessions).
/// The files live in the git-ignored folder <c>tests/LocalData/</c> and are never committed:
/// <code>
/// tests/LocalData/pdf-layouts.json        the layouts file (the same format as "Import layouts…")
/// tests/LocalData/pdf-bills/*.pdf         real supplier bills, any file name
/// </code>
/// The test is skipped when <c>tests/LocalData/pdf-bills/</c> does not exist.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalDataFactAttribute : FactAttribute
{
    /// <summary>The folder with the local files, or null when it is not there.</summary>
    public static string? LocalDataFolder { get; } = Find();

    public LocalDataFactAttribute()
    {
        if (LocalDataFolder is null || !Directory.Exists(Path.Combine(LocalDataFolder, "pdf-bills")))
        {
            Skip = "tests/LocalData/pdf-bills/ does not exist (local-only test).";
        }
    }

    private static string? Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "LocalData");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
