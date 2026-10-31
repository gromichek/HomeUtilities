using System.Collections.ObjectModel;
using System.Globalization;
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

    [ObservableProperty]
    private string _plannedText = string.Empty;

    [ObservableProperty]
    private string _plannedSummary = string.Empty;

    [ObservableProperty]
    private bool _showPlanned;

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

    /// <summary>The reminders the app plans now (debug list; filled when the list is opened).</summary>
    public ObservableCollection<PlannedReminderRowViewModel> Planned { get; } = [];

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

    [RelayCommand]
    private Task TogglePlannedAsync() => RunSafeAsync(async () =>
    {
        if (ShowPlanned)
        {
            ShowPlanned = false;
            return;
        }

        // Hand the current plan to the system first, so the list compares the same plan with what the system holds;
        // otherwise a list opened before the debounced update (or after a data change) showed false "not handed" marks.
        if (IsEnabled && _notifications.CanSchedule)
        {
            await _scheduler.RescheduleAsync();
        }

        await LoadPlannedAsync();
        ShowPlanned = true;
    });

    private async Task LoadPlannedAsync()
    {
        var items = await _scheduler.PreviewAsync();
        var canCheck = _notifications.CanSchedule && items.Count > 0;
        IReadOnlyCollection<int> pending = canCheck ? await _notifications.GetPendingIdsAsync() : [];
        Planned.Clear();
        foreach (var item in items)
        {
            Planned.Add(new PlannedReminderRowViewModel(
                item.At.ToString("g", CultureInfo.CurrentCulture),
                _localizer["ReminderPlannedKind_" + item.Kind],
                $"{item.Title} — {item.Body}",
                item.Target,
                canCheck && !pending.Contains(item.Id) ? _localizer["ReminderPlannedNotInSystem"] : string.Empty));
        }

        PlannedSummary = !IsEnabled
            ? _localizer["ReminderPlannedOff"]
            : items.Count == 0
                ? _localizer["ReminderPlannedNone"]
                : _localizer.Get("ReminderPlannedCount", items.Count);
        if (canCheck)
        {
            // How many of the planned reminders the system really holds, and when the plan was last handed over.
            var held = items.Count(item => pending.Contains(item.Id));
            var when = _scheduler.LastScheduledAt is { } at ? at.ToString("g", CultureInfo.CurrentCulture) : "—";
            PlannedSummary += Environment.NewLine + _localizer.Get("ReminderPlannedInSystem", held, items.Count, when);
        }
    }

    /// <summary>While the list is open, a settings change reschedules at once and refreshes the list, so it never shows an old time.</summary>
    private async Task RescheduleAndRefreshAsync()
    {
        await _scheduler.RescheduleAsync();
        if (ShowPlanned)
        {
            await LoadPlannedAsync();
        }
    }

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
        PlannedText = _localizer["SettingsRemindersPlanned"];
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
        if (ShowPlanned)
        {
            _ = RunSafeAsync(RescheduleAndRefreshAsync);
        }
        else
        {
            _scheduler.RequestReschedule();
        }
    }
}

/// <summary>One line of the "Planned reminders" list.</summary>
/// <param name="When">Local date and time.</param>
/// <param name="Kind">What the reminder is about.</param>
/// <param name="Text">Title and body as the notification shows them.</param>
/// <param name="Target">The screen a tap opens.</param>
/// <param name="Problem">Empty, or a warning that the system does not hold this reminder.</param>
public sealed record PlannedReminderRowViewModel(string When, string Kind, string Text, string Target, string Problem = "")
{
    /// <summary>True if <see cref="Problem"/> has a text.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Time and kind in one line.</summary>
    public string Header => $"{When} · {Kind}";
}
