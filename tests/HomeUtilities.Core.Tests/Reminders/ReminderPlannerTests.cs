using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reminders;
using HomeUtilities.Core.Tests.Overview;

namespace HomeUtilities.Core.Tests.Reminders;

public class ReminderPlannerTests
{
    private static readonly ReminderSettings On = new(Enabled: true);

    private static DateTime Now(int month, int day, int hour = 7) => new(2026, month, day, hour, 0, 0);

    private static DateTime At(int month, int day, int hour = 9, int minute = 0) => new(2026, month, day, hour, minute, 0);

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static IReadOnlyList<PlannedReminder> Plan(OverviewScenario scenario, DateTime now, ReminderSettings? settings = null, int horizon = 45) =>
        ReminderPlanner.Plan(scenario.Data, settings ?? On, now, horizon);

    // ---- Switches -------------------------------------------------------------------------------------------------

    [Fact]
    public void Disabled_plans_nothing()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 10m, D(11, 5), new YearMonth(2026, 10));

        Assert.Empty(Plan(scenario, Now(11, 1), On with { Enabled = false }));
    }

    [Fact]
    public void Kind_switched_off_plans_nothing_of_that_kind()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 10m, D(11, 5), new YearMonth(2026, 10));
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water);

        var plan = Plan(scenario, Now(11, 1), On with { Bills = false });

        Assert.All(plan, r => Assert.Equal(ReminderKind.SubmitReadings, r.Kind));
        Assert.NotEmpty(plan);
    }

    // ---- Readings -------------------------------------------------------------------------------------------------

    [Fact]
    public void Readings_reminder_is_at_the_window_start_day_at_the_configured_time()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water);

        var reminder = Assert.Single(Plan(scenario, Now(10, 20), On with { TimeOfDay = new TimeOnly(10, 30) }, horizon: 6));

        Assert.Equal(At(10, 25, 10, 30), reminder.At);
        Assert.Equal(ReminderKind.SubmitReadings, reminder.Kind);
        Assert.Equal("ReminderReadingsBody", reminder.Body.Key);
        Assert.Equal(["Flat", "Water", 25, 28], reminder.Body.Args);
        Assert.Equal($"readings:{scenario.Flat.Id}:2026-10-25", reminder.Key);
        Assert.Equal(ReminderRoutes.ReadingsFor, reminder.Target.Route);
        Assert.Equal("2026-10-25", reminder.Target.Parameters["date"]);
        Assert.Equal(scenario.Flat.Id.ToString(), reminder.Target.Parameters["propertyId"]);
    }

    [Fact]
    public void Readings_plans_the_next_month_too_when_inside_the_horizon()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water);

        var plan = Plan(scenario, Now(10, 20));

        Assert.Equal([At(10, 25), At(10, 28), At(11, 25), At(11, 28)], plan.Select(r => r.At));
    }

    [Fact]
    public void Contracts_of_one_property_with_the_same_window_are_merged()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        var gas = scenario.AddContract("Gas", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water);
        scenario.AddMeter(gas, "Boiler");

        var plan = Plan(scenario, Now(10, 20), horizon: 6);

        var reminder = Assert.Single(plan);
        Assert.Equal("Gas, Water", reminder.Body.Args[1]);
    }

    [Fact]
    public void Same_window_in_two_properties_gives_two_reminders()
    {
        var scenario = new OverviewScenario();
        var cottage = new Property { Name = "Cottage" };
        scenario.Properties.Add(cottage);
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 28));
        scenario.AddMeter(scenario.AddContract("Water", cottage, 25, 28));

        Assert.Equal(2, Plan(scenario, Now(10, 20), horizon: 6).Count);
    }

    [Fact]
    public void Window_with_readings_entered_is_skipped()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water, "Kitchen", D(10, 26));

        var plan = Plan(scenario, Now(10, 20), horizon: 10);

        Assert.Empty(plan);
    }

    [Fact]
    public void Window_of_a_contract_that_is_switched_off_is_skipped()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water);
        water.RemindAboutReadings = false;

        Assert.Empty(Plan(scenario, Now(10, 20)));
    }

    [Fact]
    public void Archived_contract_and_contract_without_meter_are_skipped()
    {
        var scenario = new OverviewScenario();
        var archived = scenario.AddContract("Old", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(archived);
        archived.IsArchived = true;
        scenario.AddContract("No meter", windowFrom: 25, windowTo: 28);

        Assert.Empty(Plan(scenario, Now(10, 20)));
    }

    [Fact]
    public void Window_start_that_has_passed_is_skipped()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 28));

        // 26 Oct: the window started yesterday; only the last-day reminder of October and the November ones remain.
        var plan = Plan(scenario, Now(10, 26));

        Assert.Equal([At(10, 28), At(11, 25), At(11, 28)], plan.Select(r => r.At));
    }

    [Fact]
    public void Time_today_that_has_passed_is_skipped_but_a_later_time_today_is_kept()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 28));

        Assert.Equal([At(10, 28), At(11, 25), At(11, 28)], Plan(scenario, Now(10, 25, 9)).Select(r => r.At));
        Assert.Equal([At(10, 25), At(10, 28), At(11, 25), At(11, 28)], Plan(scenario, Now(10, 25, 8)).Select(r => r.At));
    }

    [Fact]
    public void Last_day_reminder_is_planned_with_its_own_key_and_text()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        var gas = scenario.AddContract("Gas", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(water);
        scenario.AddMeter(gas, "Boiler");

        // Window 25-28 Oct planned on 20 Oct: the first day and the last day.
        var plan = Plan(scenario, Now(10, 20), horizon: 10);

        Assert.Equal([At(10, 25), At(10, 28)], plan.Select(r => r.At));
        Assert.Equal([$"readings:{scenario.Flat.Id}:2026-10-25", $"readings:{scenario.Flat.Id}:2026-10-25:last"], plan.Select(r => r.Key));
        Assert.Equal("ReminderReadingsLastDayBody", plan[1].Body.Key);
        Assert.Equal(["Gas, Water"], plan[1].Body.Args);
        Assert.Equal(ReminderRoutes.ReadingsFor, plan[1].Target.Route);
        Assert.Equal("2026-10-25", plan[1].Target.Parameters["date"]);
    }

    [Fact]
    public void Last_day_reminder_remains_when_the_window_has_already_started()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 28));

        // 26 Oct, no readings yet: only 28 Oct is left of this window.
        var plan = Plan(scenario, Now(10, 26), horizon: 10);

        Assert.Equal([At(10, 28)], plan.Select(r => r.At));
    }

    [Fact]
    public void Last_day_reminder_disappears_when_readings_are_entered()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 28), "Kitchen", D(10, 26));

        Assert.Empty(Plan(scenario, Now(10, 26), horizon: 10));
    }

    [Fact]
    public void Window_crossing_the_month_has_its_last_day_in_the_next_month()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 28, windowTo: 3));

        // Window 28 Oct - 3 Nov.
        var plan = Plan(scenario, Now(10, 20), horizon: 15);

        Assert.Equal([At(10, 28), At(11, 3)], plan.Select(r => r.At));
    }

    [Fact]
    public void One_day_window_has_only_one_reminder()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 25));

        var plan = Plan(scenario, Now(10, 20), horizon: 10);

        var reminder = Assert.Single(plan);
        Assert.Equal(At(10, 25), reminder.At);
        Assert.Equal("ReminderReadingsBody", reminder.Body.Key);
    }

    // ---- Bills ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Open_bill_is_announced_two_days_before_and_on_the_due_day()
    {
        var scenario = new OverviewScenario();
        var internet = scenario.AddContract("Internet");
        var bill = scenario.AddBill(internet, 22.95m, D(11, 10), new YearMonth(2026, 11));

        var plan = Plan(scenario, Now(11, 1));

        Assert.Equal([At(11, 8), At(11, 10)], plan.Select(r => r.At));
        Assert.Equal([$"bill:{bill.Id}:lead", $"bill:{bill.Id}:due"], plan.Select(r => r.Key));
        Assert.Equal("ReminderBillBodyIn", plan[0].Body.Key);
        Assert.Equal(["Internet", 22.95m, Currency.EUR, 2], plan[0].Body.Args);
        Assert.Equal("ReminderBillBodyToday", plan[1].Body.Key);
        Assert.All(plan, r => Assert.Equal(ReminderKind.PayBill, r.Kind));
        Assert.All(plan, r => Assert.Equal(ReminderRoutes.BillEdit, r.Target.Route));
        Assert.All(plan, r => Assert.Equal(bill.Id.ToString(), r.Target.Parameters["id"]));
    }

    [Fact]
    public void One_day_lead_uses_the_tomorrow_text()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 50m, D(11, 10), new YearMonth(2026, 11));

        var plan = Plan(scenario, Now(11, 1), On with { BillLeadDays = 1 });

        Assert.Equal("ReminderBillBodyTomorrow", plan[0].Body.Key);
        Assert.Equal(At(11, 9), plan[0].At);
    }

    [Fact]
    public void Zero_lead_announces_only_the_due_day()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 50m, D(11, 10), new YearMonth(2026, 11));

        var plan = Plan(scenario, Now(11, 1), On with { BillLeadDays = 0 });

        Assert.Equal([At(11, 10)], plan.Select(r => r.At));
    }

    [Fact]
    public void Lead_is_limited_to_fourteen_days()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 50m, D(11, 30), new YearMonth(2026, 11));

        var plan = Plan(scenario, Now(11, 1), On with { BillLeadDays = 99 });

        Assert.Equal(At(11, 16), plan[0].At);
    }

    [Fact]
    public void Paid_bill_draft_and_bill_without_due_date_are_not_announced()
    {
        var scenario = new OverviewScenario();
        var paid = scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));
        scenario.AddPayment(40m, D(11, 2), property: scenario.Flat, allocations: (paid, 40m));
        scenario.AddBill(null, 40m, null, new YearMonth(2026, 11));
        var draft = scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));
        draft.IsDraft = true;
        var deleted = scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));
        deleted.IsDeleted = true;

        Assert.Empty(Plan(scenario, Now(11, 1)));
    }

    [Fact]
    public void Partly_paid_bill_is_still_announced()
    {
        var scenario = new OverviewScenario();
        var bill = scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));
        scenario.AddPayment(15m, D(11, 2), property: scenario.Flat, allocations: (bill, 15m));

        Assert.Equal(2, Plan(scenario, Now(11, 1)).Count);
        Assert.Equal(25m, Plan(scenario, Now(11, 1))[0].Body.Args[1]);
    }

    [Fact]
    public void Lead_that_has_passed_leaves_only_the_due_day()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));

        Assert.Equal([At(11, 10)], Plan(scenario, Now(11, 9)).Select(r => r.At));
    }

    [Fact]
    public void Overdue_bill_has_no_reminder()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 40m, D(10, 1), new YearMonth(2026, 9));

        Assert.Empty(Plan(scenario, Now(11, 1)));
    }

    [Fact]
    public void Template_lead_overrides_the_setting()
    {
        var scenario = new OverviewScenario();
        var template = new BillTemplate { Name = "Insurance", RemindDaysBefore = 7 };
        var bill = scenario.AddBill(null, 300m, D(11, 20), new YearMonth(2026, 11));
        bill.TemplateId = template.Id;
        var data = scenario.Data with { BillTemplates = [template] };

        var plan = ReminderPlanner.Plan(data, On, Now(11, 1));

        Assert.Equal([At(11, 13), At(11, 20)], plan.Select(r => r.At));
        Assert.Equal(7, plan[0].Body.Args[3]);
    }

    [Fact]
    public void Template_without_lead_uses_the_setting()
    {
        var scenario = new OverviewScenario();
        var template = new BillTemplate { Name = "Insurance" };
        scenario.AddBill(null, 300m, D(11, 20), new YearMonth(2026, 11)).TemplateId = template.Id;
        var data = scenario.Data with { BillTemplates = [template] };

        Assert.Equal(At(11, 18), ReminderPlanner.Plan(data, On, Now(11, 1))[0].At);
    }

    [Fact]
    public void Tax_installment_has_its_own_kind_and_text()
    {
        var scenario = new OverviewScenario();
        var bill = scenario.AddBill(null, 120m, D(11, 10), new YearMonth(2026, 11), BillKind.TaxInstallment);
        bill.Title = "Property tax 2026";

        var plan = Plan(scenario, Now(11, 1));

        Assert.All(plan, r => Assert.Equal(ReminderKind.TaxInstallment, r.Kind));
        Assert.All(plan, r => Assert.Equal("ReminderTaxTitle", r.Title.Key));
        Assert.Equal("Property tax 2026", plan[0].Body.Args[0]);
        Assert.StartsWith("tax:", plan[0].Key, StringComparison.Ordinal);
    }

    // ---- Verification ---------------------------------------------------------------------------------------------

    [Fact]
    public void Verification_is_announced_sixty_days_before_and_on_the_due_date()
    {
        var scenario = new OverviewScenario();
        var contract = scenario.AddContract("Water");
        var meter = scenario.AddMeter(contract, "Kitchen");
        meter.NextVerificationDue = D(12, 31);

        var plan = Plan(scenario, Now(10, 1), horizon: 100);

        Assert.Equal([At(11, 1), At(12, 31)], plan.Select(r => r.At));
        Assert.Equal("ReminderVerificationBodyIn", plan[0].Body.Key);
        Assert.Equal(["Kitchen", 60], plan[0].Body.Args);
        Assert.Equal("ReminderVerificationBodyToday", plan[1].Body.Key);
        Assert.Equal(ReminderRoutes.MeterEdit, plan[0].Target.Route);
        Assert.Equal(meter.Id.ToString(), plan[0].Target.Parameters["id"]);
        Assert.Equal(scenario.Flat.Id.ToString(), plan[0].Target.Parameters["propertyId"]);
    }

    [Fact]
    public void Removed_meter_has_no_verification_reminder()
    {
        var scenario = new OverviewScenario();
        var meter = scenario.AddMeter(scenario.AddContract("Water"));
        meter.NextVerificationDue = D(11, 20);
        meter.RemovedOn = D(10, 1);

        Assert.Empty(Plan(scenario, Now(10, 15)));
    }

    // ---- Drafts ---------------------------------------------------------------------------------------------------

    private static Bill Draft(OverviewScenario scenario, DateTime createdLocal)
    {
        var bill = new Bill { PropertyId = scenario.Flat.Id, IsDraft = true, Period = new YearMonth(2026, 11) };
        bill.CreatedAt = new DateTimeOffset(createdLocal, TimeZoneInfo.Local.GetUtcOffset(createdLocal));
        return bill;
    }

    [Fact]
    public void Drafts_are_announced_every_third_day_at_most_five_times()
    {
        var scenario = new OverviewScenario();
        var data = scenario.Data with { Drafts = [Draft(scenario, new DateTime(2026, 10, 1, 12, 0, 0)), Draft(scenario, new DateTime(2026, 10, 1, 12, 0, 0))] };

        var plan = ReminderPlanner.Plan(data, On, Now(11, 1));

        Assert.Equal([At(11, 1), At(11, 4), At(11, 7), At(11, 10), At(11, 13)], plan.Select(r => r.At));
        Assert.Equal(["drafts:2026-11-01", "drafts:2026-11-04", "drafts:2026-11-07", "drafts:2026-11-10", "drafts:2026-11-13"], plan.Select(r => r.Key));
        Assert.Equal(2, plan[0].Body.Args[0]);
        Assert.Equal(ReminderRoutes.Bills, plan[0].Target.Route);
        Assert.Equal("Drafts", plan[0].Target.Parameters["status"]);
        Assert.Equal(scenario.Flat.Id.ToString(), plan[0].Target.Parameters["propertyId"]);
    }

    [Fact]
    public void Drafts_of_two_properties_open_the_bills_of_all_properties()
    {
        var scenario = new OverviewScenario();
        var cottage = new Property { Name = "Cottage" };
        scenario.Properties.Add(cottage);
        var second = Draft(scenario, new DateTime(2026, 10, 1));
        second.PropertyId = cottage.Id;
        var data = scenario.Data with { Drafts = [Draft(scenario, new DateTime(2026, 10, 1)), second] };

        var plan = ReminderPlanner.Plan(data, On, Now(11, 1));

        Assert.False(plan[0].Target.Parameters.ContainsKey("propertyId"));
    }

    [Fact]
    public void Draft_that_appeared_today_after_the_time_is_announced_tomorrow()
    {
        var scenario = new OverviewScenario();
        var data = scenario.Data with { Drafts = [Draft(scenario, new DateTime(2026, 11, 1, 12, 0, 0))] };

        var plan = ReminderPlanner.Plan(data, On, Now(11, 1, 13));

        Assert.Equal(At(11, 2), plan[0].At);
    }

    [Fact]
    public void Old_draft_after_the_time_waits_for_the_next_third_day()
    {
        var scenario = new OverviewScenario();
        var data = scenario.Data with { Drafts = [Draft(scenario, new DateTime(2026, 10, 1, 12, 0, 0))] };

        var plan = ReminderPlanner.Plan(data, On, Now(11, 1, 13));

        Assert.Equal(At(11, 4), plan[0].At);
    }

    [Fact]
    public void No_drafts_no_draft_reminder()
    {
        Assert.Empty(Plan(new OverviewScenario(), Now(11, 1)));
    }

    // ---- Time of day, horizon, count -----------------------------------------------------------------------------

    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 59)]
    [InlineData(20, 59)]
    [InlineData(21, 0)]
    [InlineData(23, 30)]
    public void The_configured_time_is_used_as_it_is_also_at_night(int hour, int minute)
    {
        var at = ReminderPlanner.TimeOn(D(10, 25), new TimeOnly(hour, minute));

        Assert.Equal(At(10, 25, hour, minute), at);
    }

    [Fact]
    public void Configured_night_time_stays_on_the_due_day_in_the_plan()
    {
        // Bill due 10 Nov, lead 0 days, time 22:00 -> 10 Nov 22:00 (no quiet hours since 2026-10-31).
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));

        var plan = Plan(scenario, Now(11, 1), On with { TimeOfDay = new TimeOnly(22, 0), BillLeadDays = 0 });

        Assert.Equal([At(11, 10, 22)], plan.Select(r => r.At));
    }

    [Fact]
    public void Nothing_beyond_the_horizon_is_planned()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 40m, D(12, 20), new YearMonth(2026, 12));

        Assert.Empty(Plan(scenario, Now(11, 1)));
        Assert.Equal(2, Plan(scenario, Now(11, 1), horizon: 60).Count);
        Assert.Single(Plan(scenario, Now(11, 1), horizon: 47)); // last day 18 Dec: the lead (18 Dec) fits, the due day (20 Dec) does not
    }

    [Fact]
    public void Plan_is_sorted_and_limited_to_sixty()
    {
        var scenario = new OverviewScenario();
        for (var i = 0; i < 40; i++)
        {
            scenario.AddBill(null, 10m + i, D(11, 5).AddDays(i % 20), new YearMonth(2026, 11));
        }

        var plan = Plan(scenario, Now(11, 1));

        Assert.Equal(ReminderPlanner.MaxReminders, plan.Count);
        Assert.Equal(plan.OrderBy(r => r.At).ThenBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key), plan.Select(r => r.Key));
    }

    [Fact]
    public void Keys_are_stable_between_plans()
    {
        var scenario = new OverviewScenario();
        scenario.AddMeter(scenario.AddContract("Water", windowFrom: 25, windowTo: 28));
        scenario.AddBill(null, 40m, D(11, 10), new YearMonth(2026, 11));

        var first = Plan(scenario, Now(10, 20)).Select(r => r.Key).ToList();
        var later = Plan(scenario, Now(10, 21)).Select(r => r.Key).ToList();

        Assert.Equal(first, later);
        Assert.Equal(first.Count, first.Distinct().Count());
    }

    // ---- Target text ----------------------------------------------------------------------------------------------

    [Fact]
    public void Target_survives_serialization()
    {
        var target = new ReminderTarget(
            ReminderRoutes.ReadingsFor,
            new Dictionary<string, string> { ["propertyId"] = "a b&c", ["date"] = "2026-10-25" });

        var parsed = ReminderTarget.Parse(target.Serialize());

        Assert.NotNull(parsed);
        Assert.Equal(target.Route, parsed.Route);
        Assert.Equal(target.Parameters.OrderBy(p => p.Key), parsed.Parameters.OrderBy(p => p.Key));
    }

    [Fact]
    public void Target_without_parameters_is_just_the_route()
    {
        var target = new ReminderTarget(ReminderRoutes.Overview, new Dictionary<string, string>());

        Assert.Equal("Overview", target.Serialize());
        Assert.Equal("Overview", ReminderTarget.Parse("Overview")!.Route);
        Assert.Null(ReminderTarget.Parse(null));
        Assert.Null(ReminderTarget.Parse(" "));
    }
}
