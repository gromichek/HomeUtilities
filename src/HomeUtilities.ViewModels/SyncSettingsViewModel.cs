using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The Sync section of the Settings (T-030): connects the Google account, shows the state and the e-mail, checks the access
/// to the hidden app folder (write, list, read, delete a test file) and disconnects. Since T-031 it also switches the data sync on and off, shows its status and runs "Sync now".
/// </summary>
public sealed partial class SyncSettingsViewModel : ViewModelBase
{
    private readonly ISyncAccountService _account;
    private readonly ISyncStorage _storage;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly IErrorLog _errorLog;
    private readonly ISyncEngine? _engine;
    private readonly IAutoBackupService? _backups;
    private bool _updatingSwitch;

    private CancellationTokenSource? _signInCts;

    [ObservableProperty]
    private string _hintText = string.Empty;

    [ObservableProperty]
    private string _providerText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _accountText = string.Empty;

    /// <summary>The result of the last action: an error in plain words, or the success of the check.</summary>
    [ObservableProperty]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _connectText = string.Empty;

    [ObservableProperty]
    private string _cancelSignInText = string.Empty;

    [ObservableProperty]
    private string _checkText = string.Empty;

    [ObservableProperty]
    private string _disconnectText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    // ---- Data sync (T-031) ----

    /// <summary>The switch "Sync on this device". Changed by the user it starts the first join or switches the sync off.</summary>
    [ObservableProperty]
    private bool _syncOn;

    [ObservableProperty]
    private string _syncSwitchText = string.Empty;

    [ObservableProperty]
    private string _syncRunText = string.Empty;

    [ObservableProperty]
    private string _lastSyncText = string.Empty;

    [ObservableProperty]
    private string _syncNowText = string.Empty;

    [ObservableProperty]
    private string _detailsHeader = string.Empty;

    [ObservableProperty]
    private string _detailsText = string.Empty;

    public SyncSettingsViewModel(
        ISyncAccountService account,
        ISyncStorage storage,
        ILocalizer localizer,
        IDialogService dialogs,
        IErrorLog errorLog,
        ISyncEngine? engine = null,
        IAutoBackupService? backups = null)
    {
        _engine = engine;
        _backups = backups;
        if (engine is not null)
        {
            engine.StatusChanged += (_, _) => RefreshTexts();
        }

        _account = account;
        _storage = storage;
        _localizer = localizer;
        _dialogs = dialogs;
        _errorLog = errorLog;
        // One instance lives with the Settings page, like the account service: no unsubscribe needed.
        _account.StateChanged += (_, _) => RefreshTexts();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
            {
                RaiseAvailability();
            }
        };
        RefreshTexts();
        _ = RunSafeAsync(() => _account.InitializeAsync());
    }

    public SyncAccountState State => _account.State;

    public bool IsNotConfigured => State.Status == SyncAccountStatus.NotConfigured;

    public bool IsSignedIn => State.Status == SyncAccountStatus.SignedIn;

    public bool HasMessage => MessageText.Length > 0;

    /// <summary>"Connect" is offered when configured and not connected (also after the access was lost).</summary>
    public bool CanConnect => !IsNotConfigured && !IsSignedIn && !IsConnecting && !IsBusy;

    public bool CanCheck => IsSignedIn && !IsConnecting && !IsBusy;

    public bool CanDisconnect => (IsSignedIn || State.Status == SyncAccountStatus.Error) && !IsConnecting && !IsBusy;

    public bool ShowAccount => IsSignedIn;

    /// <summary>The data sync block is shown when an engine exists and the account is connected.</summary>
    public bool ShowDataSync => _engine is not null && IsSignedIn;

    public bool IsNotBusy => !IsBusy;

    public bool HasDetails => DetailsText.Length > 0;

    public bool CanSyncNow => ShowDataSync && SyncOn && !IsBusy && !IsConnecting;

    partial void OnDetailsTextChanged(string value) => OnPropertyChanged(nameof(HasDetails));

    partial void OnSyncOnChanged(bool value)
    {
        if (!_updatingSwitch)
        {
            _ = RunSafeAsync(() => ToggleSyncAsync(value));
        }
    }

    private void SetSwitch(bool value)
    {
        _updatingSwitch = true;
        try
        {
            SyncOn = value;
        }
        finally
        {
            _updatingSwitch = false;
        }
    }

    private async Task ToggleSyncAsync(bool on)
    {
        if (_engine is null)
        {
            return;
        }

        MessageText = string.Empty;
        HasError = false;
        if (!on)
        {
            await _engine.DisableAsync();
            RefreshTexts();
            return;
        }

        IsBusy = true;
        try
        {
            var info = await _engine.InspectAsync();
            var choice = FirstJoinChoice.StartHere;
            if (info.CloudHasData)
            {
                choice = FirstJoinChoice.Replace;
                if (info.NeedsChoice)
                {
                    var replace = _localizer["SyncJoinReplace"];
                    var merge = _localizer["SyncJoinMerge"];
                    var picked = await _dialogs.ChooseAsync(_localizer["SyncJoinQuestion"], [replace, merge]);
                    if (picked is null)
                    {
                        SetSwitch(false);
                        return;
                    }

                    choice = picked == merge ? FirstJoinChoice.Merge : FirstJoinChoice.Replace;
                    if (choice == FirstJoinChoice.Replace
                        && _backups is not null
                        && !await _backups.BackupOrConfirmAsync(_dialogs, _localizer, AutoBackupReason.BeforeRestore))
                    {
                        SetSwitch(false);
                        return;
                    }
                }
            }

            await _engine.EnableAsync(choice);
        }
        catch (SyncException ex)
        {
            ShowError(ex, "Sync.Enable");
            SetSwitch(false);
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    }

    /// <summary>Uploads and downloads now.</summary>
    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private Task SyncNowAsync() => RunSafeAsync(async () =>
    {
        if (_engine is null)
        {
            return;
        }

        MessageText = string.Empty;
        HasError = false;
        IsBusy = true;
        try
        {
            await _engine.SyncAsync(SyncTrigger.Manual);
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    });

    /// <summary>Connect is shown until connected (disabled when this build is not configured); while waiting, Cancel takes its place.</summary>
    public bool ShowConnect => !IsSignedIn && !IsConnecting;

    partial void OnMessageTextChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsConnectingChanged(bool value) => RefreshTexts();

    /// <summary>Re-reads the stored sign-in when the page opens.</summary>
    public override Task OnAppearingAsync()
    {
        RefreshTexts();
        return RunSafeAsync(() => _account.InitializeAsync());
    }

    /// <summary>Opens the system sign-in. The sign-in can be cancelled with <see cref="CancelSignIn"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => RunSafeAsync(async () =>
    {
        MessageText = string.Empty;
        HasError = false;
        using var cts = new CancellationTokenSource();
        _signInCts = cts;
        IsConnecting = true;
        try
        {
            await _account.SignInAsync(cts.Token);
        }
        catch (SyncException ex)
        {
            ShowError(ex, "Sync.SignIn");
        }
        finally
        {
            _signInCts = null;
            IsConnecting = false;
            RefreshTexts();
        }
    });

    /// <summary>Stops a sign-in that waits for the browser (the listener is closed).</summary>
    [RelayCommand]
    private void CancelSignIn() => _signInCts?.Cancel();

    /// <summary>Writes, lists, reads and deletes a small test file in the app folder.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckConnectionAsync() => RunSafeAsync(async () =>
    {
        MessageText = _localizer["SyncChecking"];
        HasError = false;
        IsBusy = true;
        try
        {
            var result = await SyncConnectionCheck.RunAsync(_storage);
            if (result.Succeeded)
            {
                MessageText = _localizer["SyncCheckOk"];
                return;
            }

            var error = result.Error!;
            var step = _localizer[$"SyncStep_{result.FailedStep}"];
            var reason = error is SyncException sync ? _localizer[ErrorKey(sync.Kind)] : _localizer[ErrorKey(SyncErrorKind.Other)];
            LogError("Sync.Check", error);
            MessageText = _localizer.Get("SyncCheckFailed", step, reason);
            HasError = true;
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    });

    /// <summary>Removes the sign-in on this device and revokes the access at the provider.</summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private Task DisconnectAsync() => RunSafeAsync(async () =>
    {
        if (!await _dialogs.ConfirmAsync(
                _localizer["SyncDisconnect"], _localizer["SyncDisconnectConfirm"], _localizer["SyncDisconnect"], _localizer["Cancel"]))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_engine is { IsEnabled: true })
            {
                await _engine.DisableAsync();
            }

            var result = await _account.SignOutAsync();
            HasError = result == SignOutResult.RevokeFailed;
            MessageText = _localizer[result == SignOutResult.Done ? "SyncDisconnected" : "SyncDisconnectedRevokeFailed"];
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    });

    protected override void RefreshTexts()
    {
        Title = _localizer["SettingsSync"];
        HintText = _localizer["SyncHint"];
        ProviderText = _localizer.Get("SyncProvider", _account.ProviderName);
        ConnectText = _localizer["SyncConnect"];
        CancelSignInText = _localizer["SyncCancelSignIn"];
        CheckText = _localizer["SyncCheck"];
        DisconnectText = _localizer["SyncDisconnect"];

        var state = State;
        StatusText = IsConnecting
            ? _localizer["SyncSigningIn"]
            : _localizer[state.Status switch
            {
                SyncAccountStatus.NotConfigured => "SyncStatusNotConfigured",
                SyncAccountStatus.SignedIn => "SyncStatusSignedIn",
                SyncAccountStatus.Error => "SyncStatusError",
                _ => "SyncStatusSignedOut",
            }];
        AccountText = state.Email is { Length: > 0 } email ? _localizer.Get("SyncAccount", email) : string.Empty;
        if (state.Status == SyncAccountStatus.Error && state.Error is { } error)
        {
            MessageText = _localizer[ErrorKey(error)];
            HasError = true;
        }

        SummaryText = state.Status switch
        {
            SyncAccountStatus.NotConfigured => _localizer["SettingsValueSyncNotConfigured"],
            SyncAccountStatus.SignedIn => state.Email ?? string.Empty,
            SyncAccountStatus.Error => _localizer["SettingsValueSyncError"],
            _ => _localizer["SettingsValueSyncSignedOut"],
        };

        RefreshDataSyncTexts();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(ShowDataSync));
        OnPropertyChanged(nameof(IsNotConfigured));
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(ShowAccount));
        RaiseAvailability();
    }

    private void RaiseAvailability()
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(ShowConnect));
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanDisconnect));
        ConnectCommand.NotifyCanExecuteChanged();
        CheckConnectionCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSyncNow));
        OnPropertyChanged(nameof(IsNotBusy));
        SyncNowCommand.NotifyCanExecuteChanged();
    }

    private void RefreshDataSyncTexts()
    {
        SyncSwitchText = _localizer["SyncSwitch"];
        SyncNowText = _localizer["SyncNow"];
        DetailsHeader = _localizer["SyncDetails"];
        if (_engine is null)
        {
            return;
        }

        if (SyncOn != _engine.IsEnabled)
        {
            SetSwitch(_engine.IsEnabled);
        }

        var status = _engine.Status;
        SyncRunText = status.Kind switch
        {
            SyncStateKind.Off => _localizer["SyncRunOff"],
            SyncStateKind.Syncing => _localizer["SyncRunSyncing"],
            SyncStateKind.WaitingForNetwork => _localizer["SyncRunWaiting"],
            SyncStateKind.UpdateRequired => _localizer["SyncRunUpdate"],
            SyncStateKind.Failed => _localizer.Get("SyncRunFailed", _localizer[ErrorKey(status.Error ?? SyncErrorKind.Other)]),
            _ => _localizer.Get("SyncRunSynced", Local(status.LastSyncAt)),
        };
        if (status.ConflictCount > 0 && status.Kind == SyncStateKind.Synced)
        {
            SyncRunText += " " + _localizer.Get("SyncConflicts", status.ConflictCount);
        }

        if (status.ClockSkew)
        {
            SyncRunText += " " + _localizer["SyncClockSkew"];
        }

        LastSyncText = status.LastSyncAt is null ? _localizer["SyncNever"] : _localizer.Get("SyncLastSync", Local(status.LastSyncAt));
        DetailsText = string.Join(Environment.NewLine, _engine.Details.Take(12).Select(d => $"{d.At.ToLocalTime():HH:mm} {d.Text}"));
    }

    private static string Local(DateTimeOffset? at) => at is { } value ? value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture) : string.Empty;

    private void ShowError(SyncException ex, string source)
    {
        // A cancelled sign-in is the user's own decision: say so, but it is no error to diagnose.
        if (ex.Kind != SyncErrorKind.Cancelled)
        {
            LogError(source, ex);
        }

        MessageText = _localizer[ErrorKey(ex.Kind)];
        HasError = ex.Kind != SyncErrorKind.Cancelled;
    }

    // Messages never contain tokens or codes (SyncException texts are fixed), so the details can go to the log as they are.
    private void LogError(string source, Exception error) => _errorLog.Append(source, error);

    private static string ErrorKey(SyncErrorKind kind) => $"SyncError_{kind}";
}
