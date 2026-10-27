using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;

namespace HomeUtilities.Core.Tests.Overview;

public class TodoBuilderTests
{
    private static DateOnly Oct(int day) => new(2026, 10, day);

    private static DateOnly Nov(int day) => new(2026, 11, day);

    // ---- Readings window 25–28 ------------------------------------------------------------------------------------

    private static (OverviewScenario Scenario, Contract Water, Meter Meter) WaterWindow(params DateOnly[] readings)
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 25, windowTo: 28);
        var meter = scenario.AddMeter(water, "Kitchen", readings);
        return (scenario, water, meter);
    }

    [Fact]
    public void Window_opens_in_three_days_is_soon()
    {
        var (scenario, water, _) = WaterWindow();

        var item = Assert.Single(scenario.Todo(Oct(22)));

        Assert.Equal(TodoKind.ReadingsWindow, item.Kind);
        Assert.Equal(TodoSeverity.Soon, item.Severity);
        Assert.Equal(Oct(25), item.Date);
        Assert.Equal(Oct(28), item.EndDate);
        Assert.Equal(water.Id, item.ContractId);
        Assert.Equal(["Water"], item.Args);
    }

    [Fact]
    public void Window_opening_in_four_days_is_not_listed_yet()
    {
        var (scenario, _, _) = WaterWindow();

        Assert.Empty(scenario.Todo(Oct(21)));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(28)]
    public void Open_window_without_readings_is_today(int day)
    {
        var (scenario, _, _) = WaterWindow();

        var item = Assert.Single(scenario.Todo(Oct(day)));

        Assert.Equal(TodoSeverity.Today, item.Severity);
    }

    [Fact]
    public void Open_window_with_a_reading_inside_it_lists_nothing()
    {
        var (scenario, _, _) = WaterWindow(Oct(26));

        Assert.Empty(scenario.Todo(Oct(28)));
    }

    [Fact]
    public void A_reading_outside_the_window_does_not_count()
    {
        var (scenario, _, _) = WaterWindow(Oct(24), Nov(1));

        Assert.Equal(TodoSeverity.Today, Assert.Single(scenario.Todo(Oct(26))).Severity);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(30)]
    [InlineData(31)]
    public void Closed_window_without_readings_is_overdue_for_three_days(int day)
    {
        var (scenario, _, _) = WaterWindow();

        var item = Assert.Single(scenario.Todo(Oct(day)));

        Assert.Equal(TodoSeverity.Overdue, item.Severity);
        Assert.Equal(Oct(25), item.Date);
    }

    [Fact]
    public void Missed_window_is_gone_four_days_after_its_end()
    {
        var (scenario, _, _) = WaterWindow();

        Assert.Empty(scenario.Todo(Nov(1)));
    }

    [Fact]
    public void Closed_window_with_readings_lists_nothing()
    {
        var (scenario, _, _) = WaterWindow(Oct(27));

        Assert.Empty(scenario.Todo(Oct(30)));
    }

    [Fact]
    public void Window_that_wraps_the_month_end_is_open_until_the_third()
    {
        var scenario = new OverviewScenario();
        var power = scenario.AddContract("Electricity", windowFrom: 28, windowTo: 3);
        scenario.AddMeter(power);

        var onNov1 = Assert.Single(scenario.Todo(Nov(1)));
        Assert.Equal((TodoSeverity.Today, Oct(28), Nov(3)), (onNov1.Severity, onNov1.Date, onNov1.EndDate));

        scenario.Meters[0].Registers[0].Readings.Add(new MeterReading { Date = Oct(30), Value = 5m });
        Assert.Empty(scenario.Todo(Nov(1)));
    }

    [Fact]
    public void Window_of_next_month_opening_in_two_days_is_soon()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", windowFrom: 1, windowTo: 3);
        scenario.AddMeter(water);

        var item = Assert.Single(scenario.Todo(Oct(30)));

        Assert.Equal((TodoSeverity.Soon, Nov(1)), (item.Severity, item.Date));
    }

    [Fact]
    public void Contracts_with_the_same_window_make_one_item_listing_both()
    {
        var (scenario, _, _) = WaterWindow();
        var heat = scenario.AddContract("Heating", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(heat, "Bathroom");

        var item = Assert.Single(scenario.Todo(Oct(26)));

        Assert.Equal(["Heating", "Water"], item.Args);
        Assert.Null(item.ContractId);
    }

    [Fact]
    public void Only_the_contracts_still_missing_readings_are_listed()
    {
        var (scenario, _, _) = WaterWindow(Oct(26));
        var heat = scenario.AddContract("Heating", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(heat, "Bathroom");

        var item = Assert.Single(scenario.Todo(Oct(27)));

        Assert.Equal(["Heating"], item.Args);
        Assert.Equal(heat.Id, item.ContractId);
    }

    [Fact]
    public void A_meter_shared_by_two_contracts_satisfies_both()
    {
        var (scenario, water, meter) = WaterWindow(Oct(26));
        var heat = scenario.AddContract("Heating", windowFrom: 25, windowTo: 28);
        scenario.Links.Add(new ContractMeter { ContractId = heat.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });

        Assert.Empty(scenario.Todo(Oct(27)));
        Assert.NotEqual(water.Id, heat.Id);
    }

    [Fact]
    public void Different_windows_of_one_property_are_separate_items()
    {
        var (scenario, _, _) = WaterWindow();
        var power = scenario.AddContract("Electricity", windowFrom: 26, windowTo: 28);
        scenario.AddMeter(power, "Hall");

        Assert.Equal(2, scenario.Todo(Oct(26)).Count);
    }

    [Fact]
    public void Contracts_without_window_meter_or_validity_or_archived_are_ignored()
    {
        var scenario = new OverviewScenario();
        scenario.AddContract("No window");
        scenario.AddMeter(scenario.AddContract("Window but archived", windowFrom: 25, windowTo: 28));
        scenario.Contracts[^1].IsArchived = true;
        scenario.AddContract("Window but no meter", windowFrom: 25, windowTo: 28);
        scenario.AddMeter(scenario.AddContract("Ended", windowFrom: 25, windowTo: 28));
        scenario.Contracts[^1].ValidTo = Oct(1);

        Assert.Empty(scenario.Todo(Oct(26)));
    }

    // ---- Bills due -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(28, TodoSeverity.Soon)]
    [InlineData(27, TodoSeverity.Today)]
    [InlineData(26, TodoSeverity.Overdue)]
    [InlineData(20, TodoSeverity.Soon)]
    public void Open_bill_is_listed_by_its_due_date(int dueDay, TodoSeverity expected)
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water");
        var bill = scenario.AddBill(water, 45.10m, Oct(dueDay), new YearMonth(2026, 9));
        if (dueDay == 20)
        {
            bill.DueOn = Nov(3); // seven days ahead
        }

        var item = Assert.Single(scenario.Todo(Oct(27)));

        Assert.Equal(TodoKind.BillDue, item.Kind);
        Assert.Equal(expected, item.Severity);
        Assert.Equal(bill.Id, item.BillId);
        Assert.Equal(45.10m, item.Amount);
        Assert.Equal(["Water"], item.Args);
        Assert.Equal(new YearMonth(2026, 9), item.Period);
    }

    [Fact]
    public void Bill_due_in_eight_days_is_not_listed_yet()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(scenario.AddContract(), 10m, Nov(4), new YearMonth(2026, 9));

        Assert.Empty(scenario.Todo(Oct(27)));
    }

    [Fact]
    public void Partly_paid_bill_shows_the_open_amount()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        var bill = scenario.AddBill(water, 50m, Oct(30), new YearMonth(2026, 9));
        scenario.AddPayment(20m, Oct(20), water, null, (bill, 20m));

        Assert.Equal(30m, Assert.Single(scenario.Todo(Oct(27))).Amount);
    }

    [Fact]
    public void Paid_bill_refund_bill_bill_without_due_date_and_deleted_bill_are_not_listed()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        var paid = scenario.AddBill(water, 50m, Oct(20), new YearMonth(2026, 9));
        scenario.AddPayment(50m, Oct(19), water, null, (paid, 50m));
        scenario.AddBill(water, -12.40m, Oct(20), new YearMonth(2026, 8), BillKind.Settlement);
        scenario.AddBill(water, 30m, null, new YearMonth(2026, 7));
        scenario.AddBill(water, 30m, Oct(20), new YearMonth(2026, 6)).IsDeleted = true;

        Assert.Empty(scenario.Todo(Oct(27)));
    }

    [Fact]
    public void A_deleted_payment_does_not_close_the_bill()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        var bill = scenario.AddBill(water, 50m, Oct(20), new YearMonth(2026, 9));
        scenario.AddPayment(50m, Oct(19), water, null, (bill, 50m)).IsDeleted = true;

        Assert.Equal(50m, Assert.Single(scenario.Todo(Oct(27))).Amount);
    }

    [Fact]
    public void Tax_installment_is_its_own_kind_with_the_bill_title()
    {
        var scenario = new OverviewScenario();
        var tax = scenario.AddContract("Property tax", mode: BillingMode.Assessed);
        var bill = scenario.AddBill(tax, 96.40m, Oct(30), new YearMonth(2026, 10), BillKind.TaxInstallment);
        bill.Title = "Property tax 2026, installment 3 of 4";

        var item = Assert.Single(scenario.Todo(Oct(27)));

        Assert.Equal(TodoKind.TaxInstallmentDue, item.Kind);
        Assert.Equal(["Property tax 2026, installment 3 of 4"], item.Args);
    }

    [Fact]
    public void One_off_bill_without_contract_uses_its_title()
    {
        var scenario = new OverviewScenario();
        scenario.AddBill(null, 186.45m, Oct(26), new YearMonth(2026, 10), BillKind.OneOff);

        Assert.Equal(["Repair"], Assert.Single(scenario.Todo(Oct(27))).Args);
    }

    // ---- Bill missing --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(25, true)]
    public void Missing_bill_appears_from_the_bill_day_of_the_month_after_the_period(int day, bool listed)
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", billDay: 10);

        var items = scenario.Todo(Oct(day));

        Assert.Equal(listed, items.Count == 1);
        if (listed)
        {
            var item = items[0];
            Assert.Equal((TodoKind.BillMissing, TodoSeverity.Info), (item.Kind, item.Severity));
            Assert.Equal(new YearMonth(2026, 9), item.Period);
            Assert.Equal(Oct(10), item.Date);
            Assert.Equal(water.Id, item.ContractId);
        }
    }

    [Fact]
    public void Entered_bill_of_the_expected_period_hides_the_item_but_another_period_does_not()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", billDay: 10);
        scenario.AddBill(water, 40m, Nov(20), new YearMonth(2026, 8));
        Assert.Single(scenario.Todo(Oct(12)));

        scenario.AddBill(water, 40m, Nov(20), new YearMonth(2026, 9));
        Assert.Empty(scenario.Todo(Oct(12)));
    }

    [Fact]
    public void A_settlement_bill_does_not_replace_the_regular_bill()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract("Water", billDay: 10);
        scenario.AddBill(water, 8m, Nov(20), new YearMonth(2026, 9), BillKind.Settlement);

        Assert.Single(scenario.Todo(Oct(12)));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public void Prepaid_contract_expects_next_months_bill_from_the_bill_day_of_this_month(int day, bool listed)
    {
        var scenario = new OverviewScenario();
        scenario.AddContract("Internet", billDay: 10, timing: BillingTiming.InAdvance);

        var items = scenario.Todo(Oct(day));

        Assert.Equal(listed, items.Count == 1);
        if (listed)
        {
            Assert.Equal(new YearMonth(2026, 11), items[0].Period);
            Assert.Equal(Oct(10), items[0].Date);
        }
    }

    [Fact]
    public void Bill_day_beyond_the_month_length_means_the_last_day()
    {
        var scenario = new OverviewScenario();
        scenario.AddContract("Water", billDay: 31);

        // Period 10.2026 arrives in November, which has 30 days.
        Assert.Empty(scenario.Todo(new DateOnly(2026, 11, 29)));
        var item = Assert.Single(scenario.Todo(new DateOnly(2026, 11, 30)));
        Assert.Equal(new YearMonth(2026, 10), item.Period);
    }

    [Fact]
    public void Missing_bill_ignores_tax_archived_new_ended_contracts_and_contracts_without_bill_day()
    {
        var scenario = new OverviewScenario();
        scenario.AddContract("Tax", billDay: 10, mode: BillingMode.Assessed);
        scenario.AddContract("No bill day");
        scenario.AddContract("Archived", billDay: 10).IsArchived = true;
        scenario.AddContract("Started in October", billDay: 10, validFrom: Oct(5));
        scenario.AddContract("Ended in August", billDay: 10).ValidTo = new DateOnly(2026, 8, 31);

        Assert.Empty(scenario.Todo(Oct(12)));
    }

    [Theory]
    [InlineData(BillingMode.Actual)]
    [InlineData(BillingMode.Fixed)]
    [InlineData(BillingMode.Equalized)]
    public void Missing_bill_applies_to_actual_fixed_and_equalized_contracts(BillingMode mode)
    {
        var scenario = new OverviewScenario();
        scenario.AddContract("Contract", billDay: 10, mode: mode);

        Assert.Single(scenario.Todo(Oct(12)));
    }

    // ---- Verification and settlement -----------------------------------------------------------------------------

    [Theory]
    [InlineData(60, TodoSeverity.Soon)]
    [InlineData(1, TodoSeverity.Soon)]
    [InlineData(0, TodoSeverity.Soon)]
    [InlineData(-1, TodoSeverity.Overdue)]
    public void Verification_due_within_sixty_days_or_past_is_listed(int daysAhead, TodoSeverity expected)
    {
        var scenario = new OverviewScenario();
        var meter = new Meter { PropertyId = scenario.Flat.Id, Location = "Kitchen cold", NextVerificationDue = Oct(27).AddDays(daysAhead), InstalledOn = new DateOnly(2020, 1, 1) };
        scenario.Meters.Add(meter);

        var item = Assert.Single(scenario.Todo(Oct(27)));

        Assert.Equal((TodoKind.VerificationDue, expected), (item.Kind, item.Severity));
        Assert.Equal(meter.Id, item.MeterId);
        Assert.Equal(Oct(27).AddDays(daysAhead), item.Date);
        Assert.Equal(["Kitchen cold"], item.Args);
    }

    [Fact]
    public void Verification_is_not_listed_after_sixty_days_for_removed_meters_or_without_a_due_date()
    {
        var scenario = new OverviewScenario();
        scenario.Meters.Add(new Meter { PropertyId = scenario.Flat.Id, NextVerificationDue = Oct(27).AddDays(61) });
        scenario.Meters.Add(new Meter { PropertyId = scenario.Flat.Id, NextVerificationDue = Oct(1), RemovedOn = Oct(2) });
        scenario.Meters.Add(new Meter { PropertyId = scenario.Flat.Id });

        Assert.Empty(scenario.Todo(Oct(27)));
    }

    [Theory]
    [InlineData(1.00, true)]
    [InlineData(0.99, false)]
    [InlineData(-8.35, true)]
    [InlineData(-0.50, false)]
    public void Expected_settlement_is_listed_from_one_euro(double expected, bool listed)
    {
        var scenario = new OverviewScenario();
        var power = scenario.AddContract("Cottage electricity", mode: BillingMode.Equalized);
        scenario.Settlements[power.Id] = (decimal)expected;

        var items = scenario.Todo(Oct(27));

        Assert.Equal(listed, items.Count == 1);
        if (listed)
        {
            Assert.Equal((TodoKind.SettlementExpected, TodoSeverity.Info, (decimal)expected), (items[0].Kind, items[0].Severity, items[0].Amount!.Value));
        }
    }

    [Fact]
    public void Settlement_of_a_contract_that_is_not_equalized_or_archived_is_ignored()
    {
        var scenario = new OverviewScenario();
        scenario.Settlements[scenario.AddContract("Actual").Id] = 20m;
        var archived = scenario.AddContract("Archived", mode: BillingMode.Equalized);
        archived.IsArchived = true;
        scenario.Settlements[archived.Id] = 20m;

        Assert.Empty(scenario.Todo(Oct(27)));
    }

    // ---- Order and properties ------------------------------------------------------------------------------------

    [Fact]
    public void Items_are_ordered_overdue_today_soon_info_then_by_date_then_by_property_name()
    {
        var scenario = new OverviewScenario();
        var cottage = new Property { Name = "Cottage" };
        scenario.Properties.Add(cottage);
        var water = scenario.AddContract("Water");
        var power = scenario.AddContract("Power", cottage);
        var fixedPay = scenario.AddContract("Garage", billDay: 20, mode: BillingMode.Fixed);

        var soonLater = scenario.AddBill(water, 1m, Nov(2), new YearMonth(2026, 9));
        var overdueRecent = scenario.AddBill(water, 2m, Oct(25), new YearMonth(2026, 8));
        var overdueOld = scenario.AddBill(power, 3m, Oct(10), new YearMonth(2026, 7));
        var todayFlat = scenario.AddBill(water, 4m, Oct(27), new YearMonth(2026, 6));
        var todayCottage = scenario.AddBill(power, 5m, Oct(27), new YearMonth(2026, 5));
        var soonEarly = scenario.AddBill(water, 6m, Oct(28), new YearMonth(2026, 4));

        var items = scenario.Todo(Oct(27));

        Assert.Equal(
            [overdueOld.Id, overdueRecent.Id, todayCottage.Id, todayFlat.Id, soonEarly.Id, soonLater.Id],
            items.Where(i => i.BillId is not null).Select(i => i.BillId!.Value));
        Assert.Equal(TodoKind.BillMissing, items[^1].Kind);
        Assert.Equal(fixedPay.Id, items[^1].ContractId);
        Assert.Equal(TodoSeverity.Info, items[^1].Severity);
    }

    [Fact]
    public void Items_of_an_archived_property_are_not_listed()
    {
        var scenario = new OverviewScenario();
        var water = scenario.AddContract();
        scenario.AddBill(water, 10m, Oct(20), new YearMonth(2026, 9));
        scenario.Properties.Clear();

        Assert.Empty(scenario.Todo(Oct(27)));
    }

    [Fact]
    public void Empty_data_gives_an_empty_list()
    {
        Assert.Empty(TodoBuilder.Build(OverviewData.Empty, Oct(27)));
    }
}
