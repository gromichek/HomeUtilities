using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// "Clean up the database…" in Settings → Backup (T-037): shows what would be removed, makes the backup copy, removes the
/// deleted records and the unused attachment files for good, and — when sync is on — replaces the cloud data with the cleaned
/// data, or switches sync off for a re-join so that stale cloud changes can never bring the purged rows back.
/// </summary>
public sealed partial class DatabaseCleanUpViewModel : ViewModelBase
{
    private readonly IDatabaseCleaner _cleaner;
    private readonly IAutoBackupService _backups;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _localizer;
    private readonly ISnackbarService _snackbar;
    private readonly IErrorLog _errorLog;
    private readonly ISyncEngine? _engine;

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _hint = string.Empty;

    [ObservableProperty]
    private string _buttonText = string.Empty;

    /// <summary>Creates the view model; <paramref name="engine"/> is null where there is no sync.</summary>
    public DatabaseCleanUpViewModel(
        IDatabaseCleaner cleaner,
        IAutoBackupService backups,
        IDialogService dialogs,
        ILocalizer localizer,
        ISnackbarService snackbar,
        IErrorLog errorLog,
        ISyncEngine? engine = null)
    {
        _cleaner = cleaner;
        _backups = backups;
        _dialogs = dialogs;
        _localizer = localizer;
        _snackbar = snackbar;
        _errorLog = errorLog;
        _engine = engine;
        RefreshTexts();
    }

    /// <summary>Counts, asks, backs up, cleans, and takes care of the sync (see the class remarks).</summary>
    [RelayCommand]
    private Task CleanUpAsync() => RunSafeAsync(async () =>
    {
        if (IsBusy)
        {
            return;
        }

        var title = _localizer["CleanUpTitle"];
        IsBusy = true;
        try
        {
            var preview = await _cleaner.PreviewAsync();
            if (preview.IsEmpty)
            {
                await _dialogs.AlertAsync(title, _localizer["CleanUpNothing"]);
                return;
            }

            var syncOn = _engine?.IsEnabled == true;
            var message = _localizer.Get("CleanUpConfirm", preview.DeletedRecords, preview.OrphanFiles, FormatSize(preview.OrphanFileBytes));
            if (syncOn)
            {
                message += "\n\n" + _localizer["CleanUpConfirmSync"];
            }

            if (!await _dialogs.ConfirmAsync(title, message, _localizer["CleanUpAction"], _localizer["Cancel"]))
            {
                return;
            }

            // The copy comes first: nothing is touched when the user declines to go on without one.
            if (!await _backups.BackupOrConfirmAsync(_dialogs, _localizer, AutoBackupReason.BeforeCleanUp))
            {
                return;
            }

            // A new snackbar message replaces a pending "Undo" (its call completes with false): an Undo must never point at a purged row.
            _snackbar.Notify(_localizer["CleanUpRunning"]);
            var result = await _cleaner.CleanAsync();
            foreach (var line in result.KeptRecords)
            {
                _errorLog.Note("Database clean-up", line);
            }

            var report = new List<string> { Summary(result) };
            if (result.KeptRecords.Count > 0)
            {
                report.Add(_localizer.Get("CleanUpKept", result.KeptRecords.Count));
            }

            if (result.CompactionError is { } compaction)
            {
                _errorLog.Note("Database clean-up", "VACUUM failed: " + compaction);
                report.Add(_localizer.Get("CleanUpNotCompacted", compaction));
            }

            if (syncOn && _engine is not null)
            {
                report.Add(await ReplaceCloudAsync(_engine));
            }

            await _dialogs.AlertAsync(title, string.Join("\n\n", report));
        }
        finally
        {
            IsBusy = false;
        }
    });

    /// <summary>
    /// The existing "Replace the data in the cloud with this device's data" flow. If the user declines or the cloud cannot be
    /// reached, sync is switched off and marked for a re-join, so the stale cloud changes cannot bring purged rows back.
    /// </summary>
    private async Task<string> ReplaceCloudAsync(ISyncEngine engine)
    {
        var replace = await _dialogs.ConfirmAsync(
            _localizer["CleanUpTitle"],
            _localizer["CleanUpReplaceCloudQuestion"],
            _localizer["CleanUpReplaceCloudAction"],
            _localizer["SyncNotNow"]);
        if (replace)
        {
            try
            {
                await engine.EnableAsync(FirstJoinChoice.ReplaceCloud);
                return _localizer["CleanUpCloudReplaced"];
            }
            catch (SyncException ex)
            {
                _errorLog.Append("Database clean-up (replace the cloud)", ex);
                await engine.MarkNeedsRejoinAsync();
                return _localizer.Get("CleanUpCloudFailed", ex.Message);
            }
        }

        await engine.MarkNeedsRejoinAsync();
        return _localizer["CleanUpSyncOff"];
    }

    private string Summary(CleanUpResult result) => _localizer.Get(
        "CleanUpDone",
        result.RecordsRemoved,
        result.FilesRemoved,
        FormatSize(result.FileBytesRemoved),
        FormatSize(result.DatabaseBytesBefore),
        FormatSize(result.DatabaseBytesAfter));

    private string FormatSize(long bytes) => bytes >= 1024 * 1024
        ? _localizer.Get("BackupSizeMb", bytes / (1024.0 * 1024.0))
        : _localizer.Get("BackupSizeKb", Math.Max(1, bytes / 1024.0));

    protected override void RefreshTexts()
    {
        Header = _localizer["CleanUpTitle"];
        Hint = _localizer["CleanUpHint"];
        ButtonText = _localizer["CleanUpButton"];
    }
}
