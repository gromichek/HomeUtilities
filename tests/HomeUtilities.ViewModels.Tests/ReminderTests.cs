using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reminders;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Reminder scheduling, settings, the Settings card and the tap on a notification. "Now" is 1 Nov 2026, 07:00.</summary>
public class ReminderTests : IDisposable
{
    private static readonly DateTime LocalNow = new(2026, 11, 1, 7, 0, 0);

    private readonly FakeLanguageService _language = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeSnackbarService _snackbar = new();
    private readonly FakeErrorLog _errorLog = new();
    private readonly FakeNotifications _notifications = new();
    private readonly FakeLoader _loader = new();
    private readonly FakeDatabase _database = new();
    private readonly DataChangeNotifier _changes = new();
    private readonly List<ReminderScheduler> _schedulers = [];
    private readonly Property _flat = new() { Name = "Flat" };

    public ReminderTests()
    {
        _loader.Data = new OverviewData([_flat], [], [], [], [DueBill(DateOnly.FromDateTime(LocalNow).AddDays(5))], [], new Dictionary<Guid, decimal>());
    }

    public void Dispose()
    {
        foreach (var scheduler in _schedulers)
        {
            scheduler.Dispose();
        }
    }

    private Bill DueBill(DateOnly due) => new()
    {
        PropertyId = _flat.Id,
        Title = "Repair",
        Gross = 40m,
        DueOn = due,
        Period = new YearMonth(2026, 11),
    };

    private ReminderSettingsStore Store => new(_settings);

    private ReminderScheduler CreateScheduler(TimeSpan? debounce = null)
    {
        var scheduler = new ReminderScheduler(
            _database, _loader, Store, _notifications, new FakeLocalizer(_language), new TestNow(LocalNow), _snackbar, _changes, _errorLog,
            debounce ?? TimeSpan.FromMilliseconds(40));
        _schedulers.Add(scheduler);
        return scheduler;
    }

    private void TurnOn(ReminderSettings? settings = null) => Store.Save(settings ?? new ReminderSettings(Enabled: true));

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    // ---- Scheduler ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Reschedule_cancels_the_old_notifications_and_schedules_the_plan()
    {
        TurnOn();
        var scheduler = CreateScheduler();

        await scheduler.RescheduleAsync();

        Assert.Equal(1, _notifications.CancelCalls);
        var scheduled = Assert.Single(_notifications.Batches);
        Assert.Equal([new DateTime(2026, 11, 4, 9, 0, 0), new DateTime(2026, 11, 6, 9, 0, 0)], scheduled.Select(n => n.At));
        Assert.Equal("ReminderBillTitle [en]", scheduled[0].Title);
        Assert.StartsWith("ReminderBillBodyIn Repair|40|EUR|2 [en]", scheduled[0].Body, StringComparison.Ordinal);
        Assert.StartsWith("BillEdit?id=", scheduled[0].Data, StringComparison.Ordinal);
        Assert.Equal(1, _database.Calls);
    }

    [Fact]
    public async Task Reschedule_twice_gives_the_same_notifications()
    {
        TurnOn();
        var scheduler = CreateScheduler();

        await scheduler.RescheduleAsync();
        await scheduler.RescheduleAsync();

        Assert.Equal(2, _notifications.CancelCalls);
        Assert.Equal(2, _notifications.Batches.Count);
        Assert.Equal(_notifications.Batches[0], _notifications.Batches[1]);
    }

    [Fact]
    public async Task Disabled_reminders_cancel_everything_and_load_nothing()
    {
        Store.Save(new ReminderSettings(Enabled: false));
        var scheduler = CreateScheduler();

        await scheduler.RescheduleAsync();

        Assert.Equal(1, _notifications.CancelCalls);
        Assert.Empty(_notifications.Batches);
        Assert.Equal(0, _loader.Calls);
    }

    [Fact]
    public async Task Changed_data_is_rescheduled_once_after_the_debounce()
    {
        TurnOn();
        CreateScheduler();

        _changes.Notify();
        _changes.Notify();
        _changes.Notify();
        await WaitAsync(() => _notifications.Batches.Count >= 1);
        await Task.Delay(150);

        Assert.Single(_notifications.Batches);
        Assert.Equal(1, _loader.Calls);
    }

    [Fact]
    public async Task Request_reschedule_waits_for_the_debounce()
    {
        TurnOn();
        var scheduler = CreateScheduler(TimeSpan.FromMilliseconds(400));

        scheduler.RequestReschedule();
        await Task.Delay(100);
        Assert.Empty(_notifications.Batches);

        await WaitAsync(() => _notifications.Batches.Count == 1);
        Assert.Single(_notifications.Batches);
    }

    [Fact]
    public async Task Language_change_reschedules_with_the_new_texts()
    {
        TurnOn();
        CreateScheduler();

        _language.SetLanguage("ru");
        await WaitAsync(() => _notifications.Batches.Count == 1);

        Assert.Equal("ReminderBillTitle [ru]", _notifications.Batches[0][0].Title);
    }

    [Fact]
    public async Task A_failing_reschedule_is_logged_not_thrown()
    {
        TurnOn();
        _loader.Failure = new InvalidOperationException("boom");
        var scheduler = CreateScheduler();

        scheduler.RequestReschedule();
        await WaitAsync(() => _errorLog.Entries.Count == 1);

        Assert.Equal("Reminder scheduling", Assert.Single(_errorLog.Entries).Source);
    }

    [Fact]
    public async Task A_device_that_cannot_schedule_schedules_nothing()
    {
        TurnOn();
        _notifications.CanScheduleValue = false;
        var scheduler = CreateScheduler();

        await scheduler.RescheduleAsync();

        Assert.Empty(_notifications.Batches);
        Assert.Equal(0, _notifications.CancelCalls);
    }

    [Fact]
    public void Notification_id_is_stable_and_positive()
    {
        var id = ReminderScheduler.IdOf("bill:1:lead");

        Assert.Equal(id, ReminderScheduler.IdOf("bill:1:lead"));
        Assert.NotEqual(id, ReminderScheduler.IdOf("bill:1:due"));
        Assert.True(id >= 0);
    }

    // ---- Start summary (no scheduling) ----------------------------------------------------------------------------

    [Fact]
    public async Task Summary_is_shown_as_a_notification_once_a_day()
    {
        TurnOn();
        _notifications.CanScheduleValue = false;
        _loader.Data = _loader.Data with { Bills = [DueBill(new DateOnly(2026, 11, 3))] };
        var scheduler = CreateScheduler();

        await scheduler.ShowStartSummaryAsync();
        await scheduler.ShowStartSummaryAsync();

        var shown = Assert.Single(_notifications.Shown);
        Assert.Equal("ReminderSummaryTitle [en]", shown.Title);
        Assert.Equal("ReminderSummaryBody 1 [en]", shown.Body);
        Assert.Equal("Overview", shown.Data);
        Assert.Empty(_snackbar.Messages);
    }

    [Fact]
    public async Task Summary_falls_back_to_the_snackbar_when_notifications_are_unavailable()
    {
        TurnOn();
        _notifications.CanScheduleValue = false;
        _notifications.SupportedValue = false;
        _loader.Data = _loader.Data with { Bills = [DueBill(new DateOnly(2026, 11, 3))] };
        var scheduler = CreateScheduler();

        await scheduler.ShowStartSummaryAsync();

        Assert.Empty(_notifications.Shown);
        Assert.Single(_snackbar.Messages);
    }

    [Fact]
    public async Task Summary_is_not_shown_when_nothing_is_due_or_reminders_are_off_or_scheduling_works()
    {
        TurnOn();
        _notifications.CanScheduleValue = false;
        var scheduler = CreateScheduler();
        await scheduler.ShowStartSummaryAsync(); // the bill is due in 5 days: "soon" is within 7 days, so this one is listed
        Assert.Single(_notifications.Shown);

        _notifications.Shown.Clear();
        Store.MarkSummaryShown(new DateOnly(2026, 10, 1));
        _loader.Data = _loader.Data with { Bills = [] };
        await scheduler.ShowStartSummaryAsync();
        Assert.Empty(_notifications.Shown);

        _loader.Data = _loader.Data with { Bills = [DueBill(new DateOnly(2026, 11, 3))] };
        Store.Save(new ReminderSettings(Enabled: false));
        await scheduler.ShowStartSummaryAsync();
        Assert.Empty(_notifications.Shown);

        TurnOn();
        _notifications.CanScheduleValue = true;
        await scheduler.ShowStartSummaryAsync();
        Assert.Empty(_notifications.Shown);
    }

    [Fact]
    public async Task Test_notification_is_shown_after_the_permission()
    {
        var scheduler = CreateScheduler();

        await scheduler.SendTestAsync();

        Assert.Equal(1, _notifications.PermissionRequests);
        Assert.Equal("ReminderTestTitle [en]", Assert.Single(_notifications.Shown).Title);
    }

    [Fact]
    public async Task Test_notification_without_permission_shows_nothing()
    {
        _notifications.PermissionGranted = false;
        var scheduler = CreateScheduler();

        await scheduler.SendTestAsync();

        Assert.Empty(_notifications.Shown);
    }

    [Fact]
    public async Task Test_notification_on_a_device_without_notifications_uses_the_snackbar()
    {
        _notifications.SupportedValue = false;
        _notifications.PermissionGranted = false;
        var scheduler = CreateScheduler();

        await scheduler.SendTestAsync();

        Assert.Empty(_notifications.Shown);
        Assert.Single(_snackbar.Messages);
    }

    // ---- Settings -------------------------------------------------------------------------------------------------

    [Fact]
    public void Defaults_are_reminders_off_every_kind_on_nine_o_clock_two_days()
    {
        var settings = Store.Load();

        Assert.False(settings.Enabled);
        Assert.True(settings.Readings && settings.Bills && settings.Verification && settings.Drafts);
        Assert.Equal(new TimeOnly(9, 0), settings.Time);
        Assert.Equal(2, settings.LeadDays);
    }

    [Fact]
    public void Settings_survive_a_save_and_load()
    {
        var saved = new ReminderSettings(true, false, true, false, true, new TimeOnly(18, 45), 5);

        Store.Save(saved);

        Assert.Equal(saved, Store.Load());
    }

    [Fact]
    public void Damaged_stored_values_fall_back_to_the_defaults()
    {
        _settings.Values[ReminderSettingsStore.TimeKey] = "soon";
        _settings.Values[ReminderSettingsStore.LeadDaysKey] = "x";
        _settings.Values[ReminderSettingsStore.EnabledKey] = "maybe";

        var settings = Store.Load();

        Assert.False(settings.Enabled);
        Assert.Equal(new TimeOnly(9, 0), settings.Time);
        Assert.Equal(2, settings.LeadDays);
    }

    [Fact]
    public void Stored_lead_is_limited_to_fourteen_days()
    {
        _settings.Values[ReminderSettingsStore.LeadDaysKey] = "40";

        Assert.Equal(14, Store.Load().LeadDays);
    }

    // ---- Settings card --------------------------------------------------------------------------------------------

    private ReminderSettingsViewModel CreateCard(FakeDialogService? dialogs = null) =>
        new(Store, CreateScheduler(), _notifications, dialogs ?? new FakeDialogService(), new FakeLocalizer(_language));

    [Fact]
    public async Task Switching_on_asks_for_the_permission_stores_and_reschedules()
    {
        var card = CreateCard();

        card.IsEnabled = true;
        await WaitAsync(() => _notifications.Batches.Count == 1);

        Assert.Equal(1, _notifications.PermissionRequests);
        Assert.True(Store.Load().Enabled);
        Assert.Single(_notifications.Batches);
    }

    [Fact]
    public async Task Refused_permission_switches_back_off_and_says_why()
    {
        _notifications.PermissionGranted = false;
        var dialogs = new FakeDialogService();
        var card = CreateCard(dialogs);

        card.IsEnabled = true;
        await WaitAsync(() => dialogs.Alerts.Count == 1);

        Assert.False(card.IsEnabled);
        Assert.False(Store.Load().Enabled);
        Assert.Equal("SettingsRemindersPermissionDenied [en]", dialogs.Alerts[0].Message);
    }

    [Fact]
    public void On_a_device_without_notifications_the_switch_needs_no_permission()
    {
        _notifications.SupportedValue = false;
        _notifications.CanScheduleValue = false;
        var card = CreateCard();

        card.IsEnabled = true;

        Assert.True(card.IsNotSupported);
        Assert.Equal(0, _notifications.PermissionRequests);
        Assert.True(Store.Load().Enabled);
    }

    [Fact]
    public void Changes_of_kinds_time_and_lead_are_stored()
    {
        TurnOn();
        var card = CreateCard();

        card.ReadingsOn = false;
        card.Time = new TimeSpan(20, 15, 0);
        card.LeadDays = 4;

        var settings = Store.Load();
        Assert.False(settings.Readings);
        Assert.Equal(new TimeOnly(20, 15), settings.Time);
        Assert.Equal(4, settings.LeadDays);
        Assert.Equal("ReminderLeadDays 4 [en]", card.LeadDaysLabel);
    }

    [Fact]
    public void Lead_is_limited_to_zero_to_fourteen()
    {
        var card = CreateCard();

        card.LeadDays = 30;
        Assert.Equal(14, card.LeadDays);
        card.LeadDays = -3;
        Assert.Equal(0, card.LeadDays);
    }

    [Fact]
    public void Card_loads_the_stored_settings_without_writing_them_back()
    {
        Store.Save(new ReminderSettings(true, true, false, true, true, new TimeOnly(7, 30), 3));
        var before = new Dictionary<string, string>(_settings.Values);

        var card = CreateCard();

        Assert.True(card.IsEnabled);
        Assert.False(card.BillsOn);
        Assert.Equal(new TimeSpan(7, 30, 0), card.Time);
        Assert.Equal(3, card.LeadDays);
        Assert.Equal(before, _settings.Values);
    }

    [Fact]
    public async Task Test_button_sends_a_test_notification()
    {
        var card = CreateCard();

        await card.SendTestCommand.ExecuteAsync(null);

        Assert.Single(_notifications.Shown);
    }

    [Fact]
    public async Task Planned_list_hands_the_plan_to_the_system_and_shows_it()
    {
        TurnOn();
        var card = CreateCard();

        await card.TogglePlannedCommand.ExecuteAsync(null);

        Assert.True(card.ShowPlanned);
        Assert.Equal(2, card.Planned.Count);
        Assert.Equal(new DateTime(2026, 11, 4, 9, 0, 0).ToString("g", System.Globalization.CultureInfo.CurrentCulture), card.Planned[0].When);
        Assert.Equal("ReminderPlannedKind_PayBill [en]", card.Planned[0].Kind);
        Assert.StartsWith("BillEdit?id=", card.Planned[0].Target, StringComparison.Ordinal);
        Assert.StartsWith("ReminderPlannedCount", card.PlannedSummary, StringComparison.Ordinal);
        Assert.Single(_notifications.Batches);

        await card.TogglePlannedCommand.ExecuteAsync(null);

        Assert.False(card.ShowPlanned);
    }

    [Fact]
    public async Task Planned_list_marks_reminders_the_system_does_not_hold()
    {
        TurnOn();
        var card = CreateCard();

        // The system refuses the first reminder (4 Nov, the bill 3 days ahead): only that row is marked, 1 of 2 held.
        _notifications.RefuseFirst = true;
        await card.TogglePlannedCommand.ExecuteAsync(null);

        Assert.True(card.Planned[0].HasProblem);
        Assert.False(card.Planned[1].HasProblem);
        Assert.Equal("ReminderPlannedNotInSystem [en]", card.Planned[0].Problem);
        var handedOver = LocalNow.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Contains($"ReminderPlannedInSystem 1|2|{handedOver}", card.PlannedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Planned_list_after_scheduling_shows_all_held_and_the_time_of_the_hand_over()
    {
        TurnOn();
        var scheduler = CreateScheduler();
        var card = new ReminderSettingsViewModel(Store, scheduler, _notifications, new FakeDialogService(), new FakeLocalizer(_language));
        await scheduler.RescheduleAsync();

        await card.TogglePlannedCommand.ExecuteAsync(null);

        Assert.All(card.Planned, row => Assert.False(row.HasProblem));
        var handedOver = LocalNow.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Contains($"ReminderPlannedInSystem 2|2|{handedOver}", card.PlannedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changing_the_time_while_the_list_is_open_reschedules_and_refreshes_the_list()
    {
        TurnOn();
        var card = CreateCard();
        await card.TogglePlannedCommand.ExecuteAsync(null);

        // 09:00 -> 18:39: the bill reminder on 4 Nov moves to 18:39 at once, without closing and reopening the list.
        var expected = new DateTime(2026, 11, 4, 18, 39, 0).ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        card.Time = new TimeSpan(18, 39, 0);
        await WaitAsync(() => _notifications.Batches.Count == 2 && card.Planned[0].When == expected);

        Assert.Equal(2, _notifications.Batches.Count);
        Assert.Equal(expected, card.Planned[0].When);
        Assert.False(card.Planned[0].HasProblem);
    }

    [Fact]
    public async Task Planned_list_says_so_when_the_reminders_are_off()
    {
        var card = CreateCard();

        await card.TogglePlannedCommand.ExecuteAsync(null);

        Assert.Empty(card.Planned);
        Assert.StartsWith("ReminderPlannedOff", card.PlannedSummary, StringComparison.Ordinal);
    }

    // ---- Tap on a notification ------------------------------------------------------------------------------------

    private (NotificationTapHandler Handler, FakeNavigationService Navigation, BillFilterState Filter) CreateHandler()
    {
        var navigation = new FakeNavigationService();
        var filter = new BillFilterState { CategoryId = Guid.NewGuid() };
        return (new NotificationTapHandler(navigation, filter, new TestNow(LocalNow)), navigation, filter);
    }

    [Fact]
    public async Task Tap_on_a_readings_reminder_opens_the_readings_of_the_property_at_the_date()
    {
        var (handler, navigation, _) = CreateHandler();
        var propertyId = Guid.NewGuid();
        var target = new ReminderTarget(
            ReminderRoutes.ReadingsFor,
            new Dictionary<string, string> { ["propertyId"] = propertyId.ToString(), ["date"] = "2026-11-25" });

        await handler.OpenAsync(target.Serialize());

        var (route, parameters) = Assert.Single(navigation.Visited);
        Assert.Equal(Routes.ReadingsFor, route);
        Assert.Equal(propertyId, parameters![EditorNavigation.PropertyIdParameter]);
        Assert.Equal(new DateOnly(2026, 11, 25), parameters[ReadingsViewModel.DateParameter]);
    }

    [Fact]
    public async Task Tap_on_a_bill_reminder_opens_the_bill()
    {
        var (handler, navigation, _) = CreateHandler();
        var bill = DueBill(new DateOnly(2026, 11, 6));
        var target = ReminderPlanner.Plan(_loader.Data with { Bills = [bill] }, new ReminderSettings(true), LocalNow)[0].Target;

        await handler.OpenAsync(target.Serialize());

        var (route, parameters) = Assert.Single(navigation.Visited);
        Assert.Equal(Routes.BillEdit, route);
        Assert.Equal(bill.Id, parameters![EditorNavigation.IdParameter]);
    }

    [Fact]
    public async Task Tap_on_a_verification_reminder_opens_the_meter()
    {
        var (handler, navigation, _) = CreateHandler();
        var meterId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var target = new ReminderTarget(
            ReminderRoutes.MeterEdit,
            new Dictionary<string, string> { ["id"] = meterId.ToString(), ["propertyId"] = propertyId.ToString() });

        await handler.OpenAsync(target.Serialize());

        var (route, parameters) = Assert.Single(navigation.Visited);
        Assert.Equal(Routes.MeterEdit, route);
        Assert.Equal(meterId, parameters![EditorNavigation.IdParameter]);
        Assert.Equal(propertyId, parameters[EditorNavigation.PropertyIdParameter]);
    }

    [Fact]
    public async Task Tap_on_a_drafts_reminder_opens_the_bills_filtered_to_drafts()
    {
        var (handler, navigation, filter) = CreateHandler();
        var propertyId = Guid.NewGuid();
        var target = new ReminderTarget(
            ReminderRoutes.Bills,
            new Dictionary<string, string> { ["status"] = "Drafts", ["propertyId"] = propertyId.ToString() });

        await handler.OpenAsync(target.Serialize());

        Assert.Equal(Routes.Bills, Assert.Single(navigation.Visited).Route);
        Assert.Equal(BillStatusFilter.Drafts, filter.Status);
        Assert.Equal(propertyId, filter.PropertyId);
        Assert.Null(filter.CategoryId);
        Assert.Equal(PeriodScope.AllYears, filter.Period!.Value.Scope);
    }

    [Fact]
    public async Task Drafts_of_several_properties_clear_the_property_filter()
    {
        var (handler, _, filter) = CreateHandler();
        filter.PropertyId = Guid.NewGuid();

        await handler.OpenAsync(new ReminderTarget(ReminderRoutes.Bills, new Dictionary<string, string> { ["status"] = "Drafts" }).Serialize());

        Assert.Null(filter.PropertyId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nonsense?x=1")]
    [InlineData("BillEdit?id=not-a-guid")]
    [InlineData("ReadingsFor?propertyId=nope&date=2026-11-25")]
    public async Task Damaged_or_unknown_data_opens_the_overview(string? data)
    {
        var (handler, navigation, _) = CreateHandler();

        await handler.OpenAsync(data);

        Assert.Equal(Routes.Overview, Assert.Single(navigation.Visited).Route);
    }

    [Fact]
    public void Core_route_names_equal_the_navigation_routes()
    {
        Assert.Equal(Routes.Overview, ReminderRoutes.Overview);
        Assert.Equal(Routes.ReadingsFor, ReminderRoutes.ReadingsFor);
        Assert.Equal(Routes.BillEdit, ReminderRoutes.BillEdit);
        Assert.Equal(Routes.MeterEdit, ReminderRoutes.MeterEdit);
        Assert.Equal(Routes.Bills, ReminderRoutes.Bills);
        Assert.Equal(EditorNavigation.IdParameter, ReminderRoutes.IdParameter);
        Assert.Equal(EditorNavigation.PropertyIdParameter, ReminderRoutes.PropertyIdParameter);
        Assert.Equal(ReadingsViewModel.DateParameter, ReminderRoutes.DateParameter);
        Assert.Equal(BillsViewModel.StatusParameter, ReminderRoutes.StatusParameter);
        Assert.Equal(nameof(BillStatusFilter.Drafts), "Drafts");
    }

    // ---- Fakes ----------------------------------------------------------------------------------------------------

    internal sealed class TestNow(DateTime local) : IClock
    {
        public DateOnly Today => DateOnly.FromDateTime(local);

        public DateTimeOffset UtcNow => new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
    }

    internal sealed class FakeNotifications : INotificationService
    {
        public bool SupportedValue { get; set; } = true;

        public bool CanScheduleValue { get; set; } = true;

        public bool PermissionGranted { get; set; } = true;

        public int PermissionRequests { get; private set; }

        public int CancelCalls { get; private set; }

        public List<IReadOnlyList<ScheduledNotification>> Batches { get; } = [];

        public List<(string Title, string Body, string Data)> Shown { get; } = [];

        public bool IsSupported => SupportedValue;

        public bool CanSchedule => CanScheduleValue;

        public Task<bool> RequestPermissionAsync()
        {
            PermissionRequests++;
            return Task.FromResult(PermissionGranted);
        }

        public Task ScheduleAsync(IReadOnlyList<ScheduledNotification> notifications)
        {
            Batches.Add(notifications);
            Pending.UnionWith(notifications.Skip(RefuseFirst ? 1 : 0).Select(n => n.Id));
            return Task.CompletedTask;
        }

        public Task CancelAllAsync()
        {
            CancelCalls++;
            Pending.Clear();
            return Task.CompletedTask;
        }

        /// <summary>If true, the "system" does not accept the first notification of each batch.</summary>
        public bool RefuseFirst { get; set; }

        /// <summary>What the "system" holds: the ids of the batches scheduled since the last cancel.</summary>
        public HashSet<int> Pending { get; } = [];

        public Task<IReadOnlyCollection<int>> GetPendingIdsAsync() => Task.FromResult<IReadOnlyCollection<int>>([.. Pending]);

        public Task ShowNowAsync(string title, string body, string data)
        {
            Shown.Add((title, body, data));
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeLoader : IOverviewDataLoader
    {
        public OverviewData Data { get; set; } = OverviewData.Empty;

        public Exception? Failure { get; set; }

        public int Calls { get; private set; }

        public Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default)
        {
            Calls++;
            return Failure is null ? Task.FromResult(Data) : Task.FromException<OverviewData>(Failure);
        }
    }

    internal sealed class FakeDatabase : IDatabaseInitializer
    {
        public int Calls { get; private set; }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
