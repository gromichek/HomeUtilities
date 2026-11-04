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
        // The app's own choice dialog (UI_GUIDELINES §3a): the whole explanation is readable, not clipped like an action sheet.
        var picked = await dialogs.ChooseWithMessageAsync(
            localizer,
            localizer["SyncCloudReplacedTitle"],
            localizer["SyncCloudReplacedQuestion"],
            [
                new SingleChoiceOption(true, localizer["SyncCloudReplacedAccept"], localizer["SyncBackupFirstHint"]),
                new SingleChoiceOption(false, localizer["SyncNotNow"]),
            ],
            true);
        if (picked is not true)
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

    /// <summary>
    /// The first-join question of a device with data on both sides (<paramref name="rejoin"/> false: Replace, Replace the cloud,
    /// Merge) or after a restore / import (<paramref name="rejoin"/> true: the first two). Null when the user cancelled.
    /// </summary>
    internal static async Task<FirstJoinChoice?> AskJoinChoiceAsync(IDialogService dialogs, ILocalizer localizer, bool rejoin)
    {
        var options = new List<SingleChoiceOption>
        {
            new(FirstJoinChoice.Replace, localizer["SyncJoinReplace"], localizer["SyncBackupFirstHint"]),
            new(FirstJoinChoice.ReplaceCloud, localizer["SyncJoinReplaceCloud"], localizer["SyncReplaceCloudHint"]),
        };
        if (!rejoin)
        {
            options.Add(new SingleChoiceOption(FirstJoinChoice.Merge, localizer["SyncJoinMerge"], localizer["SyncMergeHint"]));
        }

        var picked = await dialogs.ChooseWithMessageAsync(
            localizer,
            localizer[rejoin ? "SyncRejoinTitle" : "SyncJoinTitle"],
            localizer[rejoin ? "SyncRejoinQuestion" : "SyncJoinQuestion"],
            options,
            FirstJoinChoice.Replace);
        return picked as FirstJoinChoice?;
    }
}
