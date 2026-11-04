using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// "The data in the cloud was replaced on another device — replace this device's data?" (C9). Shared by the Sync page and the
/// automatic question after a sync, so both ask the same thing and make the backup copy first.
/// </summary>
internal static class SyncReplaceFlow
{
    /// <summary>Asks, makes the backup copy and replaces the local data with the cloud data. False when the user said no.</summary>
    internal static async Task<bool> AskAndReplaceAsync(ISyncEngine engine, IDialogService dialogs, ILocalizer localizer, IAutoBackupService? backups)
    {
        if (!await dialogs.ConfirmAsync(
                localizer["SyncCloudReplacedTitle"], localizer["SyncCloudReplacedQuestion"], localizer["SyncCloudReplacedAccept"], localizer["SyncNotNow"]))
        {
            return false;
        }

        if (backups is not null && !await backups.BackupOrConfirmAsync(dialogs, localizer, AutoBackupReason.BeforeRestore))
        {
            return false;
        }

        await engine.EnableAsync(FirstJoinChoice.Replace);
        return true;
    }
}
