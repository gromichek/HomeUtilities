namespace HomeUtilities.ViewModels;

/// <summary>Locations of the app's files; implemented by the platform layer.</summary>
public interface IAppPaths
{
    /// <summary>Full path of the SQLite database file.</summary>
    string DatabasePath { get; }

    /// <summary>Full path of the error log (<c>logs/errors.log</c> in the app data folder).</summary>
    string ErrorLogPath { get; }
}
