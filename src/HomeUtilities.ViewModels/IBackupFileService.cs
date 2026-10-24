namespace HomeUtilities.ViewModels;

/// <summary>Where a backup was put.</summary>
/// <param name="Saved">False if the owner cancelled the save dialog or share sheet.</param>
/// <param name="Location">Folder or file path when the platform knows it (null after sharing).</param>
public sealed record BackupSaveResult(bool Saved, string? Location);

/// <summary>Moves backup files between the app and the places the owner chooses; implemented per platform.</summary>
public interface IBackupFileService
{
    /// <summary>
    /// Lets the owner put a backup somewhere outside the app (share sheet on Android, save dialog on Windows).
    /// <paramref name="write"/> produces the zip content.
    /// </summary>
    Task<BackupSaveResult> SaveAsync(string fileName, Func<Stream, Task> write);

    /// <summary>Lets the owner pick a backup file; returns a readable, seekable stream or null if cancelled.</summary>
    Task<Stream?> PickAsync();
}
