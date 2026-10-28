namespace HomeUtilities.ViewModels;

/// <summary>Where a saved file (backup, report export) was put.</summary>
/// <param name="Saved">False if the owner cancelled the save dialog or share sheet.</param>
/// <param name="Location">Folder or file path when the platform knows it (null after sharing).</param>
public sealed record FileSaveResult(bool Saved, string? Location);

/// <summary>Lets the owner put any file somewhere outside the app; implemented per platform.</summary>
public interface IFileSaveService
{
    /// <summary>
    /// Offers the file through the system save dialog (Android: choose a folder such as Downloads or Drive; Windows: the file dialog).
    /// When the dialog fails, Android shares the file and Windows saves it to <c>Documents\HomeUtilities\&lt;folder&gt;</c>.
    /// <paramref name="write"/> produces the content.
    /// </summary>
    /// <param name="fileName">The suggested file name.</param>
    /// <param name="contentType">The media type, e.g. <c>application/zip</c>.</param>
    /// <param name="folder">The fallback folder name below <c>Documents\HomeUtilities</c>, e.g. <c>Backups</c> or <c>Reports</c>.</param>
    /// <param name="write">Writes the content to the stream.</param>
    Task<FileSaveResult> SaveAsync(string fileName, string contentType, string folder, Func<Stream, Task> write);
}

/// <summary>Moves backup files between the app and the places the owner chooses; implemented per platform.</summary>
public interface IBackupFileService
{
    /// <summary>
    /// Lets the owner put a backup somewhere outside the app (share sheet on Android, save dialog on Windows).
    /// <paramref name="write"/> produces the zip content.
    /// </summary>
    Task<FileSaveResult> SaveAsync(string fileName, Func<Stream, Task> write);

    /// <summary>Lets the owner pick a backup file; returns a readable, seekable stream or null if cancelled.</summary>
    Task<Stream?> PickAsync();
}
