using HomeUtilities.Core.Backup;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The safety copy before a risky operation (T-028i): when it cannot be made, the user decides whether to go on without it.
/// </summary>
public static class BackupGuard
{
    /// <summary>
    /// Makes the automatic copy for <paramref name="reason"/>. Returns true when the operation may continue: the copy was made,
    /// or it failed and the user chose "Continue". Returns false when the copy failed and the user cancelled.
    /// </summary>
    public static async Task<bool> BackupOrConfirmAsync(
        this IAutoBackupService backups,
        IDialogService dialogs,
        ILocalizer localizer,
        AutoBackupReason reason)
    {
        if (await backups.BackupBeforeAsync(reason))
        {
            return true;
        }

        var why = backups.LastError ?? localizer["BackupFailedUnknownReason"];
        return await dialogs.ConfirmAsync(
            localizer["BackupFailedTitle"],
            localizer.Get("BackupFailedContinue", why),
            localizer["BackupFailedContinueAction"],
            localizer["Cancel"]);
    }
}
