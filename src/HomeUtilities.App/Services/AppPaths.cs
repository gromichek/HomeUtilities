using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class AppPaths : IAppPaths
{
    public string DatabasePath { get; } = Path.Combine(FileSystem.AppDataDirectory, "homeutilities.db");

    public string ErrorLogPath { get; } = Path.Combine(FileSystem.AppDataDirectory, "logs", "errors.log");

    /// <summary>Staging area of a restore: <c>pending.zip</c>, the wipe marker and temporary files.</summary>
    public string RestoreFolder { get; } = Path.Combine(FileSystem.AppDataDirectory, "restore");

    /// <summary>Copies of the data that a restore replaced (<c>before-restore-*.zip</c>).</summary>
    public string BackupsFolder { get; } = Path.Combine(FileSystem.AppDataDirectory, "backups");

    /// <summary>Holds only the newest automatic copy; the one folder of the Android system backup (see the backup rules).</summary>
    public string SystemBackupFolder { get; } = Path.Combine(FileSystem.AppDataDirectory, "backups", "system-copy");

    public string AttachmentsFolder { get; } = Path.Combine(FileSystem.AppDataDirectory, "attachments");
}
