using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Reminders;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The "Reminders" card of the Settings: on/off, which kinds, the time of day, the lead of bill reminders and a test
/// notification. Every change is stored and the notifications are rescheduled (debounced).
/// </summary>
public sealed partial class ReminderSettingsViewModel : ViewModelBase
{
    private readonly ReminderSettingsStore _store;
    private readonly ReminderScheduler _scheduler;
    private readonly INotificationService _notifications;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _localizer;

    private bool _loading;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _readingsOn = true;

    [ObservableProperty]
    private bool _billsOn = true;

    [ObservableProperty]
    private bool _verificationOn = true;

    [ObservableProperty]
    private bool _draftsOn = true;

    [ObservableProperty]
    private TimeSpan _time = ReminderSettings.DefaultTime.ToTimeSpan();

    [ObservableProperty]
    private int _leadDays = ReminderSettings.DefaultBillLeadDays;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _hintText = string.Empty;

    [ObservableProperty]
    private string _enabledLabel = string.Empty;

    [ObservableProperty]
    private string _readingsLabel = string.Empty;

    [ObservableProperty]
    private string _billsLabel = string.Empty;

    [ObservableProperty]
    private string _verificationLabel = string.Empty;

    [ObservableProperty]
    private string _draftsLabel = string.Empty;

    [ObservableProperty]
    private string _timeLabel = string.Empty;

    [ObservableProperty]
    private string _leadDaysLabel = string.Empty;

    [ObservableProperty]
    private string _sendTestText = string.Empty;

    [ObservableProperty]
    private string _notSupportedText = string.Empty;

    /// <summary>Creates the card and loads the stored settings.</summary>
    public ReminderSettingsViewModel(
        ReminderSettingsStore store,
        ReminderScheduler scheduler,
        INotificationService notifications,
        IDialogService dialogs,
        ILocalizer localizer)
    {
        _store = store;
        _scheduler = scheduler;
        _notifications = notifications;
        _dialogs = dialogs;
        _localizer = localizer;
        Load();
        RefreshTexts();
    }

    /// <summary>True if the device cannot show notifications at all (the card says reminders appear when the app is opened).</summary>
    public bool IsNotSupported => !_notifications.IsSupported;

    /// <summary>The detail switches and the time are shown only while the reminders are on.</summary>
    public bool ShowDetails => IsEnabled;

    /// <summary>Reads the stored settings into the fields without writing them back.</summary>
    public void Load()
    {
        _loading = true;
        try
        {
            var settings = _store.Load();
            IsEnabled = settings.Enabled;
            ReadingsOn = settings.Readings;
            BillsOn = settings.Bills;
            VerificationOn = settings.Verification;
            DraftsOn = settings.Drafts;
            Time = settings.Time.ToTimeSpan();
            LeadDays = settings.LeadDays;
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand]
    private Task SendTestAsync() => RunSafeAsync(_scheduler.SendTestAsync);

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetails));
        if (_loading)
        {
            return;
        }

        if (!value)
        {
            Persist();
            return;
        }

        _ = RunSafeAsync(EnableAsync);
    }

    partial void OnReadingsOnChanged(bool value) => Persist();

    partial void OnBillsOnChanged(bool value) => Persist();

    partial void OnVerificationOnChanged(bool value) => Persist();

    partial void OnDraftsOnChanged(bool value) => Persist();

    partial void OnTimeChanged(TimeSpan value) => Persist();

    partial void OnLeadDaysChanged(int value)
    {
        if (value is < 0 or > ReminderSettings.MaxBillLeadDays)
        {
            LeadDays = Math.Clamp(value, 0, ReminderSettings.MaxBillLeadDays);
            return;
        }

        LeadDaysLabel = _localizer.Get("ReminderLeadDays", value);
        Persist();
    }

    protected override void RefreshTexts()
    {
        HeaderText = _localizer["SettingsReminders"];
        HintText = _localizer["SettingsRemindersHint"];
        EnabledLabel = _localizer["SettingsRemindersEnabled"];
        ReadingsLabel = _localizer["SettingsRemindersReadings"];
        BillsLabel = _localizer["SettingsRemindersBills"];
        VerificationLabel = _localizer["SettingsRemindersVerification"];
        DraftsLabel = _localizer["SettingsRemindersDrafts"];
        TimeLabel = _localizer["SettingsRemindersTime"];
        LeadDaysLabel = _localizer.Get("ReminderLeadDays", LeadDays);
        SendTestText = _localizer["SettingsRemindersTest"];
        NotSupportedText = _localizer["SettingsRemindersNotSupported"];
    }

    /// <summary>Asks for the notification permission when the reminders are switched on; without it the switch goes back to off.</summary>
    private async Task EnableAsync()
    {
        if (_notifications.IsSupported && !await _notifications.RequestPermissionAsync())
        {
            _loading = true;
            try
            {
                IsEnabled = false;
            }
            finally
            {
                _loading = false;
            }

            await _dialogs.AlertAsync(_localizer["SettingsReminders"], _localizer["SettingsRemindersPermissionDenied"]);
            return;
        }

        Persist();
    }

    private void Persist()
    {
        if (_loading)
        {
            return;
        }

        _store.Save(new ReminderSettings(IsEnabled, ReadingsOn, BillsOn, VerificationOn, DraftsOn, TimeOnly.FromTimeSpan(Time), LeadDays));
        _scheduler.RequestReschedule();
    }
}
