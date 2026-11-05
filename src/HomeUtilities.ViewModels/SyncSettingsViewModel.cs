using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The Sync section of the Settings (T-030): connects the Google account, shows the state and the e-mail, checks the access
/// to the hidden app folder (write, list, read, delete a test file) and disconnects. Since T-031 it also switches the data sync on and off, shows its status and runs "Sync now".
/// </summary>
public sealed partial class SyncSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISyncAccountService _account;
    private readonly ISyncStorage _storage;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly IErrorLog _errorLog;
    private readonly ISyncEngine? _engine;
    private readonly IAutoBackupService? _backups;
    private readonly SyncSettings? _syncSettings;
    private readonly IUiDispatcher? _dispatcher;
    private bool _updatingSwitch;
    private bool _updatingOptions;
    private bool _subscribed;

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

    // ---- T-032: messages, mobile data, replace, files in the cloud ----

    [ObservableProperty]
    private bool _showMessages = true;

    [ObservableProperty]
    private string _showMessagesText = string.Empty;

    [ObservableProperty]
    private bool _downloadOnMobile;

    [ObservableProperty]
    private string _downloadOnMobileText = string.Empty;

    [ObservableProperty]
    private string _replaceFromCloudText = string.Empty;

    [ObservableProperty]
    private string _cloudFilesHeader = string.Empty;

    [ObservableProperty]
    private string _cloudFilesButtonText = string.Empty;

    /// <summary>The read-only list "Files in the cloud" as text lines; empty until loaded.</summary>
    [ObservableProperty]
    private string _cloudFilesText = string.Empty;

    public SyncSettingsViewModel(
        ISyncAccountService account,
        ISyncStorage storage,
        ILocalizer localizer,
        IDialogService dialogs,
        IErrorLog errorLog,
        ISyncEngine? engine = null,
        IAutoBackupService? backups = null,
        SyncSettings? syncSettings = null,
        IUiDispatcher? dispatcher = null)
    {
        _engine = engine;
        _backups = backups;
        _syncSettings = syncSettings;
        _dispatcher = dispatcher;
        if (syncSettings is not null)
        {
            _updatingOptions = true;
            ShowMessages = syncSettings.ShowMessages;
            DownloadOnMobile = syncSettings.DownloadOnMobileData;
            _updatingOptions = false;
        }

        _account = account;
        _storage = storage;
        _localizer = localizer;
        _dialogs = dialogs;
        _errorLog = errorLog;
        Subscribe();
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

    // The engine and the account service are singletons raising events on the sync thread (C11): every handler goes to the UI
    // thread first, and the page unsubscribes when it is left (a transient view model must not stay alive through the singleton).
    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        if (_engine is not null)
        {
            _engine.StatusChanged += OnEngineChanged;
        }

        _account.StateChanged += OnEngineChanged;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        if (_engine is not null)
        {
            _engine.StatusChanged -= OnEngineChanged;
        }

        _account.StateChanged -= OnEngineChanged;
    }

    private void OnEngineChanged(object? sender, EventArgs e)
    {
        if (_dispatcher is null)
        {
            RefreshTexts();
        }
        else
        {
            _dispatcher.Post(() =>
            {
                if (_subscribed)
                {
                    RefreshTexts();
                }
            });
        }
    }

    public void Dispose() => Unsubscribe();

    /// <inheritdoc />
    public override Task OnDisappearingAsync()
    {
        Unsubscribe();
        return base.OnDisappearingAsync();
    }

    public SyncAccountState State => _account.State;

    public bool ShowReplaceFromCloud => _engine is { Status.Kind: SyncStateKind.CloudReplaced };

    public bool HasCloudFiles => CloudFilesText.Length > 0;

    partial void OnCloudFilesTextChanged(string value) => OnPropertyChanged(nameof(HasCloudFiles));

    partial void OnShowMessagesChanged(bool value)
    {
        if (!_updatingOptions && _syncSettings is not null)
        {
            _syncSettings.ShowMessages = value;
        }
    }

    partial void OnDownloadOnMobileChanged(bool value)
    {
        if (!_updatingOptions && _syncSettings is not null)
        {
            _syncSettings.DownloadOnMobileData = value;
        }
    }

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

    /// <summary>The details list is collapsed until the owner opens it (R-43).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailsList))]
    private bool _detailsExpanded;

    /// <summary>The lines are shown only while the section is expanded.</summary>
    public bool ShowDetailsList => HasDetails && DetailsExpanded;

    [RelayCommand]
    private void ToggleDetails() => DetailsExpanded = !DetailsExpanded;

    public bool CanSyncNow => ShowDataSync && SyncOn && !IsBusy && !IsConnecting;

    partial void OnDetailsTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(ShowDetailsList));
    }

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
            if (info.CanResume)
            {
                // C14: switched off before on this account with this data: continue, no question.
                choice = FirstJoinChoice.Resume;
            }
            else if (info.CloudReplaced)
            {
                if (!await SyncReplaceFlow.AskAndReplaceAsync(_engine, _dialogs, _localizer, _backups))
                {
                    SetSwitch(false);
                }

                return;
            }
            else if (info.CloudHasData && (info.NeedsRejoin || info.NeedsChoice))
            {
                // C9: after a restore / import only the two replace directions; a first join with data on both sides also offers Merge.
                var picked = await SyncReplaceFlow.AskJoinChoiceAsync(_dialogs, _localizer, rejoin: info.NeedsRejoin);
                if (picked is null)
                {
                    SetSwitch(false);
                    return;
                }

                choice = picked.Value;
                if (choice == FirstJoinChoice.Replace
                    && _backups is not null
                    && !await _backups.BackupOrConfirmAsync(_dialogs, _localizer, AutoBackupReason.BeforeRestore))
                {
                    SetSwitch(false);
                    return;
                }
            }
            else if (info.CloudHasData)
            {
                // This device holds no data of its own: the cloud data is simply downloaded.
                choice = FirstJoinChoice.Replace;
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

    /// <summary>Replaces this device's data with the cloud data after another device replaced the cloud (C9).</summary>
    [RelayCommand(CanExecute = nameof(ShowReplaceFromCloud))]
    private Task ReplaceFromCloudAsync() => RunSafeAsync(async () =>
    {
        if (_engine is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await SyncReplaceFlow.AskAndReplaceAsync(_engine, _dialogs, _localizer, _backups);
        }
        catch (SyncException ex)
        {
            ShowError(ex, "Sync.Replace");
        }
        finally
        {
            IsBusy = false;
            RefreshTexts();
        }
    });

    /// <summary>Reads the file list of the cloud folder (read-only, C12).</summary>
    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private Task LoadCloudFilesAsync() => RunSafeAsync(async () =>
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
            CloudFilesText = SyncCloudFilesFormatter.Format(await _engine.ListCloudFilesAsync(), _localizer);
        }
        catch (SyncException ex)
        {
            ShowError(ex, "Sync.CloudFiles");
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
        Subscribe();
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
            if (_engine is not null)
            {
                // Disconnect forgets the join: switching on again later is a first join (C14).
                await _engine.ForgetJoinAsync();
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
        DisconnectText = _localizer["SyncDisconnectButton"];

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
        OnPropertyChanged(nameof(ShowReplaceFromCloud));
        SyncNowCommand.NotifyCanExecuteChanged();
        LoadCloudFilesCommand.NotifyCanExecuteChanged();
        ReplaceFromCloudCommand.NotifyCanExecuteChanged();
    }

    private void RefreshDataSyncTexts()
    {
        SyncSwitchText = _localizer["SyncSwitch"];
        SyncNowText = _localizer["SyncNow"];
        DetailsHeader = _localizer["SyncDetails"];
        ShowMessagesText = _localizer["SyncShowMessages"];
        DownloadOnMobileText = _localizer["SyncDownloadOnMobile"];
        ReplaceFromCloudText = _localizer["SyncReplaceFromCloud"];
        CloudFilesHeader = _localizer["SyncCloudFiles"];
        CloudFilesButtonText = _localizer["SyncCloudFilesShow"];
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
            SyncStateKind.CloudReplaced => _localizer["SyncRunCloudReplaced"],
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
        DetailsText = SyncDetailText.Lines(_localizer, _engine.Details.Take(12));
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
