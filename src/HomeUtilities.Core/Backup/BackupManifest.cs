namespace HomeUtilities.Core.Backup;

/// <summary>The <c>manifest.json</c> of a backup file.</summary>
/// <param name="FormatVersion">Version of the backup file layout; 1 for now.</param>
/// <param name="AppVersion">Version of the app that created the backup.</param>
/// <param name="LastMigration">Id of the last EF migration applied to the stored database.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="Platform">Platform of the device that created the backup (e.g. Android, WinUI).</param>
/// <param name="PropertyCount">Number of properties (not deleted) in the backup.</param>
/// <param name="ReadingCount">Number of meter readings (not deleted) in the backup.</param>
/// <param name="AttachmentCount">Number of attachment files in the backup.</param>
public sealed record BackupManifest(
    int FormatVersion,
    string AppVersion,
    string LastMigration,
    DateTimeOffset CreatedAt,
    string Platform,
    int PropertyCount,
    int ReadingCount,
    int AttachmentCount)
{
    /// <summary>The only backup layout this app version writes and reads.</summary>
    public const int CurrentFormatVersion = 1;
}
