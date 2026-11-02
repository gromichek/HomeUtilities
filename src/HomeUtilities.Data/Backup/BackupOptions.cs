namespace HomeUtilities.Data.Backup;

/// <summary>Where the backup service finds the app's data and keeps its staging files.</summary>
/// <param name="DatabasePath">The SQLite database file.</param>
/// <param name="AttachmentsFolder">Folder of the attachment files.</param>
/// <param name="RestoreFolder">Folder for the staged restore (<c>pending.zip</c>), the wipe marker and temporary files.</param>
/// <param name="BackupsFolder">Folder for the <c>before-restore-*.zip</c> copies of the replaced data.</param>
/// <param name="AppVersion">Version written to the manifest.</param>
/// <param name="Platform">Platform name written to the manifest.</param>
/// <param name="SystemCopyFolder">
/// Folder that holds only the newest automatic copy, for the Android system backup (Auto Backup); null on other platforms.
/// </param>
public sealed record BackupOptions(
    string DatabasePath,
    string AttachmentsFolder,
    string RestoreFolder,
    string BackupsFolder,
    string AppVersion,
    string Platform,
    string? SystemCopyFolder = null)
{
    /// <summary>Folder of the automatic copies (<c>auto</c> below <see cref="BackupsFolder"/>).</summary>
    public string AutoBackupsFolder => Path.Combine(BackupsFolder, "auto");
}
