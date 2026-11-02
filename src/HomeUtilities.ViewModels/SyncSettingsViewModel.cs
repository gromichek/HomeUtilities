using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The Sync section of the Settings (T-030): connects the Google account, shows the state and the e-mail, checks the access
/// to the hidden app folder (write, list, read, delete a test file) and disconnects. Data sync itself comes later (T-031).
/// </summary>
public sealed partial class SyncSettingsViewModel : ViewModelBase
{
    private readonly ISyncAccountService _account;
    private readonly ISyncStorage _storage;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly IErrorLog _errorLog;

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

    public SyncSettingsViewModel(
        ISyncAccountService account,
        ISyncStorage storage,
        ILocalizer localizer,
        IDialogService dialogs,
        IErrorLog errorLog)
    {
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

        OnPropertyChanged(nameof(State));
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
    }

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
