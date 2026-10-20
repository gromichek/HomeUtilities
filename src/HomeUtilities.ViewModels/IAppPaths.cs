namespace HomeUtilities.ViewModels;

/// <summary>Locations of the app's files; implemented by the platform layer.</summary>
public interface IAppPaths
{
    /// <summary>Full path of the SQLite database file.</summary>
    string DatabasePath { get; }
}
