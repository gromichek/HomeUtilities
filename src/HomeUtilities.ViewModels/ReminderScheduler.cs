using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reminders;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Keeps the system notifications in step with the data: plans the reminders (<see cref="ReminderPlanner"/>), cancels the
/// old notifications and schedules the new ones. Nothing runs while the app is closed — the plan is computed when the app
/// is open (start, resume, after a change, after a language change) and handed to the system. Registered as a singleton.
/// </summary>
public sealed class ReminderScheduler : IRecipient<LanguageChangedMessage>, IDisposable
{
    /// <summary>How long changes are collected before the notifications are rescheduled.</summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    private readonly IDatabaseInitializer _database;
    private readonly IOverviewDataLoader _loader;
    private readonly ReminderSettingsStore _settings;
    private readonly INotificationService _notifications;
    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly ISnackbarService _snackbar;
    private readonly IDataChangeNotifier _changes;
    private readonly IErrorLog _errorLog;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();

    private CancellationTokenSource? _pending;

    /// <summary>Creates the scheduler and starts listening to data and language changes.</summary>
    public ReminderScheduler(
        IDatabaseInitializer database,
        IOverviewDataLoader loader,
        ReminderSettingsStore settings,
        INotificationService notifications,
        ILocalizer localizer,
        IClock clock,
        ISnackbarService snackbar,
        IDataChangeNotifier changes,
        IErrorLog errorLog,
        TimeSpan? debounce = null)
    {
        _database = database;
        _loader = loader;
        _settings = settings;
        _notifications = notifications;
        _localizer = localizer;
        _clock = clock;
        _snackbar = snackbar;
        _changes = changes;
        _errorLog = errorLog;
        Debounce = debounce ?? DefaultDebounce;
        _changes.Changed += OnDataChanged;
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>The debounce interval.</summary>
    public TimeSpan Debounce { get; }

    /// <summary>Local time the plan was last handed to the system (null before the first run in this app session).</summary>
    public DateTime? LastScheduledAt { get; private set; }

    /// <summary>A stable positive notification id for a reminder key (FNV-1a), so the same reminder always has the same id.</summary>
    public static int IdOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var hash = 2166136261u;
        foreach (var c in key)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    /// <summary>Reschedules after <see cref="Debounce"/>; several requests within that time give one run.</summary>
    public void RequestReschedule()
    {
        CancellationTokenSource source;
        lock (_debounceLock)
        {
            _pending?.Cancel();
            _pending?.Dispose();
            source = _pending = new CancellationTokenSource();
        }

        _ = DebouncedAsync(source.Token);
    }

    /// <summary>Replaces the pending notifications by the current plan; with the reminders switched off it cancels them all.</summary>
    public async Task RescheduleAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var settings = _settings.Load();
            if (!settings.Enabled)
            {
                await _notifications.CancelAllAsync();
                return;
            }

            if (!_notifications.CanSchedule)
            {
                return;
            }

            var notifications = (await PlanAsync(settings, ct))
                .Select(r => new ScheduledNotification(IdOf(r.Key), r.At, Localize(r.Title), Localize(r.Body), r.Target.Serialize()))
                .ToList();

            await _notifications.CancelAllAsync();
            await _notifications.ScheduleAsync(notifications);
            LastScheduledAt = _clock.UtcNow.ToLocalTime().DateTime;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The reminders that would be scheduled now, with the texts as the notification shows them ("Planned reminders" in the
    /// Settings, for checking). Empty while the reminders are off. Nothing is scheduled or cancelled.
    /// </summary>
    public async Task<IReadOnlyList<PlannedReminderInfo>> PreviewAsync(CancellationToken ct = default)
    {
        var settings = _settings.Load();
        if (!settings.Enabled)
        {
            return [];
        }

        return [.. (await PlanAsync(settings, ct)).Select(r => new PlannedReminderInfo(IdOf(r.Key), r.At, r.Kind, Localize(r.Title), Localize(r.Body), r.Target.Serialize()))];
    }

    /// <summary>
    /// On a device that cannot schedule (Windows) shows one summary of what needs attention now, at most once a day: as a
    /// system notification, or as the in-app message if notifications are not available.
    /// </summary>
    public async Task ShowStartSummaryAsync(CancellationToken ct = default)
    {
        var settings = _settings.Load();
        var today = _clock.Today;
        if (!settings.Enabled || _notifications.CanSchedule || _settings.LastSummary == today)
        {
            return;
        }

        await _database.InitializeAsync(ct);
        var data = await _loader.LoadAsync(today, ct);
        var count = TodoBuilder.Build(data, today).Count(item => item.Severity != TodoSeverity.Info && Wanted(settings, item.Kind));
        if (count == 0)
        {
            return;
        }

        _settings.MarkSummaryShown(today);
        await ShowAsync(_localizer["ReminderSummaryTitle"], _localizer.Get("ReminderSummaryBody", count));
    }

    /// <summary>Shows a notification now so the owner can check that notifications work; asks for the permission first.</summary>
    public async Task SendTestAsync()
    {
        if (!await _notifications.RequestPermissionAsync() && _notifications.IsSupported)
        {
            return;
        }

        await ShowAsync(_localizer["ReminderTestTitle"], _localizer["ReminderTestBody"]);
    }

    /// <inheritdoc />
    public void Receive(LanguageChangedMessage message) => RequestReschedule();

    /// <inheritdoc />
    public void Dispose()
    {
        _changes.Changed -= OnDataChanged;
        WeakReferenceMessenger.Default.Unregister<LanguageChangedMessage>(this);
        lock (_debounceLock)
        {
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = null;
        }

        _gate.Dispose();
    }

    private async Task<IReadOnlyList<PlannedReminder>> PlanAsync(ReminderSettings settings, CancellationToken ct)
    {
        // The first run can come before any screen opened the database.
        await _database.InitializeAsync(ct);
        var now = _clock.UtcNow.ToLocalTime().DateTime;
        var data = await _loader.LoadAsync(DateOnly.FromDateTime(now), ct);
        return ReminderPlanner.Plan(data, settings, now);
    }

    private static bool Wanted(ReminderSettings settings, TodoKind kind) => kind switch
    {
        TodoKind.ReadingsWindow => settings.Wants(ReminderKind.SubmitReadings),
        TodoKind.BillDue or TodoKind.TaxInstallmentDue => settings.Wants(ReminderKind.PayBill),
        TodoKind.VerificationDue => settings.Wants(ReminderKind.MeterVerification),
        TodoKind.DraftsToConfirm => settings.Wants(ReminderKind.DraftsToConfirm),
        _ => false,
    };

    private async Task ShowAsync(string title, string body)
    {
        if (_notifications.IsSupported)
        {
            await _notifications.ShowNowAsync(title, body, ReminderRoutes.Overview);
        }
        else
        {
            _snackbar.Notify($"{title}: {body}");
        }
    }

    private string Localize(ReminderText text) => text.Args.Count == 0 ? _localizer[text.Key] : _localizer.Get(text.Key, [.. text.Args]);

    private void OnDataChanged(object? sender, EventArgs e) => RequestReschedule();

    private async Task DebouncedAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Debounce, ct);
            await RescheduleAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errorLog.Append("Reminder scheduling", ex);
        }
    }
}

/// <summary>One planned reminder as the Settings list shows it.</summary>
/// <param name="Id">Notification id (see <see cref="ReminderScheduler.IdOf"/>).</param>
/// <param name="At">Local time it is scheduled for.</param>
/// <param name="Kind">What it is about.</param>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification text.</param>
/// <param name="Target">The serialized screen a tap opens.</param>
public sealed record PlannedReminderInfo(int Id, DateTime At, ReminderKind Kind, string Title, string Body, string Target);
