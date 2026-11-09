using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// What the user is told about the sync while the app is open (C13) and the one question that cannot wait for the Sync page:
/// "the data in the cloud was replaced on another device" (C9). Messages are short snackbars, never blocking.
/// Rules (owner, 2026-11-03): "Syncing…" only for Sync now and the first sync after the start; "Synced: N received, M sent" only
/// when something was received or sent; an error always, but the same kind only once until the next success.
/// </summary>
public sealed class SyncNotifier : IDisposable
{
    /// <summary>How long a sync message stays (one place to tune it after the owner's test).</summary>
    public static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(1.5);

    private readonly ISyncEngine _engine;
    private readonly ISnackbarService _snackbar;
    private readonly ILocalizer _localizer;
    private readonly SyncSettings _settings;
    private readonly IUiDispatcher? _dispatcher;
    private readonly IDialogService? _dialogs;
    private readonly IAutoBackupService? _backups;
    private readonly IErrorLog? _errorLog;
    private readonly HashSet<string> _shownErrors = [];
    private SyncStateKind _previous = SyncStateKind.Off;
    private bool _asking;
    private bool _asked;

    public SyncNotifier(
        ISyncEngine engine,
        ISnackbarService snackbar,
        ILocalizer localizer,
        SyncSettings settings,
        IUiDispatcher? dispatcher = null,
        IDialogService? dialogs = null,
        IAutoBackupService? backups = null,
        IErrorLog? errorLog = null)
    {
        _engine = engine;
        _snackbar = snackbar;
        _localizer = localizer;
        _settings = settings;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _backups = backups;
        _errorLog = errorLog;
        _engine.StatusChanged += OnStatusChanged;
    }

    public void Dispose() => _engine.StatusChanged -= OnStatusChanged;

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new SyncStatusChangedMessage());
        var status = _engine.Status;
        var previous = _previous;
        _previous = status.Kind;
        var message = MessageFor(status, previous);
        if (message is not null && _settings.ShowMessages)
        {
            Post(() => _ = _snackbar.ShowAsync(message, null, MessageDuration));
        }

        if (status.Kind == SyncStateKind.CloudReplaced)
        {
            Post(() => _ = AskOnceAsync());
        }
        else if (status.Kind is SyncStateKind.Synced or SyncStateKind.Off)
        {
            _asked = false;
        }
    }

    private string? MessageFor(SyncStatus status, SyncStateKind previous)
    {
        switch (status.Kind)
        {
            case SyncStateKind.Syncing:
                // The status is raised again for the same run only when it changes; "first sync after start" is trigger Start.
                return previous != SyncStateKind.Syncing && status.Trigger is SyncTrigger.Manual or SyncTrigger.Start
                    ? _localizer["SyncMsgSyncing"]
                    : null;

            case SyncStateKind.Synced:
                _shownErrors.Clear();
                return status.Received + status.Sent > 0 && status.Trigger is not null
                    ? _localizer.Get("SyncMsgSynced", status.Received, status.Sent)
                    : null;

            case SyncStateKind.WaitingForNetwork:
            case SyncStateKind.Failed:
            case SyncStateKind.UpdateRequired:
                var key = $"{status.Kind}/{status.Error}";
                if (!_shownErrors.Add(key))
                {
                    return null;
                }

                return status.Kind switch
                {
                    SyncStateKind.WaitingForNetwork => _localizer["SyncMsgWaiting"],
                    SyncStateKind.UpdateRequired => _localizer["SyncRunUpdate"],
                    _ => _localizer.Get("SyncMsgFailed", _localizer[$"SyncError_{status.Error ?? SyncErrorKind.Other}"]),
                };

            default:
                return null;
        }
    }

    private async Task AskOnceAsync()
    {
        if (_dialogs is null || _asked || _asking)
        {
            return;
        }

        _asking = true;
        try
        {
            _asked = true;
            await SyncReplaceFlow.AskAndReplaceAsync(_engine, _dialogs, _localizer, _backups);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _errorLog?.Append("Sync.CloudReplaced", ex);
        }
        finally
        {
            _asking = false;
        }
    }

    private void Post(Action action)
    {
        if (_dispatcher is null)
        {
            action();
        }
        else
        {
            _dispatcher.Post(action);
        }
    }
}
