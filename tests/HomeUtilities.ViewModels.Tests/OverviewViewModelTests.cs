using System.Globalization;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The Overview: to-do rows with their actions, the Money and This year cards, the filter. Today is 27 Oct 2026.</summary>
public class OverviewViewModelTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);
    private readonly KitOverviewLoader _loader;
    private readonly FakeInitializer _initializer = new();

    public OverviewViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        _loader = new KitOverviewLoader(_kit.Kit);
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private OverviewViewModel Create() =>
        new(_kit.Kit.Localizer, _kit.Kit.Clock, _initializer, _loader, _kit.QuickPay, _kit.Kit.Toast, _kit.Kit.Navigation, _kit.Kit.Dialogs,
            _kit.Kit.Settings, _kit.Kit.Layout);

    private async Task<OverviewViewModel> LoadAsync()
    {
        var vm = Create();
        await vm.OnAppearingAsync();
        return vm;
    }

    private static string Money(string amount) => $"{amount} {Eur}";

    /// <summary>The n-th argument of a fake-localized text ("Key a|b|c [en]") without the trailing language tag.</summary>
    private static string LastPart(string text, int index)
    {
        var parts = text.Split('|');
        var part = parts[index];
        return index == parts.Length - 1 ? part[..^" [en]".Length] : part;
    }

    // ---- Basics --------------------------------------------------------------------------------------------------

    [Fact]
    public void Texts_come_from_the_localizer_and_follow_a_language_change()
    {
        var vm = Create();

        Assert.Equal("SectionOverview [en]", vm.Title);
        Assert.StartsWith("TodayIs ", vm.TodayText, StringComparison.Ordinal);
        Assert.Equal("OverviewTodoHeader [en]", vm.TodoHeader);

        _kit.Kit.Language.SetLanguage("ru");

        Assert.Equal("SectionOverview [ru]", vm.Title);
        Assert.Equal("OverviewMoneyHeader [ru]", vm.MoneyHeader);
    }

    [Fact]
    public async Task Appearing_opens_the_database_once_and_loads_the_data_every_time()
    {
        var vm = Create();

        await vm.OnAppearingAsync();
        await vm.OnAppearingAsync();

        Assert.Equal(1, _initializer.Calls);
        Assert.Equal(2, _loader.Loads);
        Assert.True(vm.IsLoaded);
        Assert.Equal(string.Empty, vm.DatabaseStatusText);
    }

    [Fact]
    public async Task A_failing_database_shows_the_error_and_loads_nothing()
    {
        _initializer.Failure = new InvalidOperationException("disk full");
        var vm = Create();

        await vm.OnAppearingAsync();

        Assert.Equal("DatabaseError disk full [en]", vm.DatabaseStatusText);
        Assert.Equal(0, _loader.Loads);
        Assert.False(vm.IsLoaded);
    }

    [Fact]
    public async Task Nothing_to_do_shows_the_empty_state_in_every_card()
    {
        var vm = await LoadAsync();

        Assert.True(vm.ShowTodoEmpty);
        Assert.False(vm.HasTodo);
        Assert.True(vm.ShowMoneyEmpty);
        Assert.True(vm.ShowYearEmpty);
    }

    // ---- To do ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_overdue_bill_is_a_row_with_text_date_amount_and_a_paid_button()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var sep = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        _kit.AddPayment(20m, (sep, 20m));

        var vm = await LoadAsync();

        var row = Assert.Single(vm.TodoRows);
        Assert.Equal("TodoBillDue Water|09.2026 [en]", row.Text);
        Assert.Equal("Sample flat · TodoDueOverdue 20.10.2026|7 [en]", row.DetailText);
        Assert.Equal(Money("30.00"), row.AmountText);
        Assert.True(row.IsOverdue);
        Assert.True(row.IsDue);
        Assert.True(row.CanPay);
        Assert.Equal("BillMarkPaid [en]", row.PayText);
    }

    [Fact]
    public async Task Rows_follow_the_order_overdue_today_soon_info()
    {
        var water = _kit.AddWaterContract(tariff: false);
        water.BillDay = 10;
        _kit.AddBill(water, new YearMonth(2026, 8), 10m, due: new DateOnly(2026, 11, 2));
        _kit.AddBill(water, new YearMonth(2026, 7), 20m, due: new DateOnly(2026, 10, 27));
        _kit.AddBill(water, new YearMonth(2026, 6), 30m, due: new DateOnly(2026, 10, 1));

        var vm = await LoadAsync();

        Assert.Equal(
            [TodoSeverity.Overdue, TodoSeverity.Today, TodoSeverity.Soon, TodoSeverity.Info],
            vm.TodoRows.Select(r => r.Severity));
        Assert.Equal([TodoKind.BillDue, TodoKind.BillDue, TodoKind.BillDue, TodoKind.BillMissing], vm.TodoRows.Select(r => r.Kind));
    }

    [Fact]
    public async Task Paid_from_a_row_pays_the_bill_and_the_row_disappears_then_undo_brings_it_back()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var vm = await LoadAsync();
        _kit.Kit.Toast.ActionResult = (_, _) => false;

        vm.TodoRows[0].PayCommand!.Execute(null);
        await Task.Delay(50);

        Assert.Empty(vm.TodoRows);
        var payment = Assert.Single(_kit.Payments.Stored);
        Assert.Equal((50m, true), (payment.Amount, payment.CreatedByQuickAction));
        Assert.Equal(bill.Id, Assert.Single(payment.Allocations).BillId);
        Assert.Contains(_kit.Kit.Toast.Calls, c => c.Message == "BillMarkedPaid [en]" && c.Action == "BillUndo [en]");

        // Undo: the snackbar reports a tap on the action.
        _kit.Kit.Toast.ActionResult = (_, _) => true;
        await vm.MarkPaidAsync(bill.Id); // already paid: nothing happens
        Assert.Empty(vm.TodoRows);
    }

    [Fact]
    public async Task Undo_after_paid_takes_the_payment_away_and_the_row_returns()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var vm = await LoadAsync();
        _kit.Kit.Toast.ActionResult = (_, action) => action == "BillUndo [en]";

        await vm.MarkPaidAsync(bill.Id);

        Assert.Empty(_kit.Payments.Stored);
        var row = Assert.Single(vm.TodoRows);
        Assert.Equal(bill.Id, row.Item.BillId);
        Assert.Equal(Money("50.00"), row.AmountText);
    }

    [Fact]
    public async Task A_bill_row_opens_the_bill()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var vm = await LoadAsync();

        vm.TodoRows[0].OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillEdit, route);
        Assert.Equal(bill.Id, parameters![EditorNavigation.IdParameter]);
    }

    [Fact]
    public async Task A_missing_bill_row_has_an_enter_bill_button_that_prefills_contract_and_period()
    {
        var water = _kit.AddWaterContract(tariff: false);
        water.BillDay = 10;
        var vm = await LoadAsync();

        var row = Assert.Single(vm.TodoRows);
        Assert.Equal("TodoBillMissing 09.2026|Water [en]", row.Text);
        Assert.Equal("Sample flat · TodoBillMissingDetail 10.10.2026 [en]", row.DetailText);
        Assert.True(row.IsMissing);
        Assert.False(row.CanPay);
        Assert.Equal("TodoEnterBill [en]", row.EnterText);

        row.OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillEdit, route);
        Assert.Equal(water.Id, parameters![EditorNavigation.ContractIdParameter]);
        Assert.Equal(_kit.Kit.Property.Id, parameters[EditorNavigation.PropertyIdParameter]);
        Assert.Equal("2026-09", parameters[BillEditViewModel.PeriodParameter]);
    }

    [Fact]
    public async Task A_readings_row_opens_the_readings_of_the_property_for_today()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var meter = new Meter { PropertyId = _kit.Kit.Property.Id, InstalledOn = new DateOnly(2020, 1, 1) };
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id });
        _kit.Kit.Meters.Stored.Add(meter);
        _kit.Kit.Contracts.Links.Add(new ContractMeter { ContractId = water.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        var vm = await LoadAsync();

        var row = Assert.Single(vm.TodoRows);
        Assert.Equal("TodoReadings Water [en]", row.Text);
        Assert.Equal("Sample flat · TodoReadingsOpen 25–28 Oct [en]", row.DetailText);
        Assert.True(row.IsReadings && row.IsToday);

        row.OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.ReadingsFor, route);
        Assert.Equal(_kit.Kit.Property.Id, parameters![EditorNavigation.PropertyIdParameter]);
        Assert.Equal(Today, parameters[ReadingsViewModel.DateParameter]);
    }

    [Fact]
    public async Task A_verification_row_opens_the_meter()
    {
        var meter = new Meter { PropertyId = _kit.Kit.Property.Id, InstalledOn = new DateOnly(2015, 1, 1), Location = "Kitchen cold", NextVerificationDue = new DateOnly(2026, 9, 1) };
        _kit.Kit.Meters.Stored.Add(meter);
        var vm = await LoadAsync();

        var row = Assert.Single(vm.TodoRows);
        Assert.Equal("TodoVerification Kitchen cold [en]", row.Text);
        Assert.Equal("Sample flat · TodoVerificationOverdue 01.09.2026 [en]", row.DetailText);
        Assert.True(row.IsVerification && row.IsOverdue);

        row.OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.MeterEdit, route);
        Assert.Equal(meter.Id, parameters![EditorNavigation.IdParameter]);
        Assert.Equal(_kit.Kit.Property.Id, parameters[EditorNavigation.PropertyIdParameter]);
    }

    [Fact]
    public async Task A_settlement_row_opens_the_settlement_page()
    {
        var power = _kit.AddWaterContract("Cottage electricity", tariff: false);
        power.BillingMode = BillingMode.Equalized;
        _loader.Settlements[power.Id] = -8.35m;
        var vm = await LoadAsync();

        var row = Assert.Single(vm.TodoRows);
        Assert.Equal("TodoSettlement Cottage electricity [en]", row.Text);
        Assert.Equal("Sample flat · TodoSettlementRefund [en]", row.DetailText);
        Assert.Equal($"-8.35 {Eur}", row.AmountText);
        Assert.True(row.IsInfo && row.IsSettlement);

        row.OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.EqualizedSettlement, route);
        Assert.Equal(power.Id, parameters![EditorNavigation.ContractIdParameter]);
    }

    // ---- Money and This year -------------------------------------------------------------------------------------

    private (Contract Water, Bill Aug, Bill Sep, Bill Oct) AddMoneyScenario()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var aug = _kit.AddBill(water, new YearMonth(2026, 8), 40m, due: new DateOnly(2026, 9, 20));
        var sep = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var oct = _kit.AddBill(water, new YearMonth(2026, 10), 60m, due: new DateOnly(2026, 11, 20));
        _kit.AddPayment(40m, (aug, 40m));
        _kit.AddPayment(20m, (sep, 20m));
        _kit.AddPayment(22m, (oct, 0m)); // 22 of its own that no bill uses
        _kit.Payments.All[^1].Allocations.Clear();
        return (water, aug, sep, oct);
    }

    [Fact]
    public async Task Money_card_lists_open_overdue_prepayment_and_balance_of_the_property()
    {
        AddMoneyScenario();

        var vm = await LoadAsync();

        var block = Assert.Single(vm.MoneyBlocks);
        Assert.Equal("Sample flat", block.Header);
        Assert.Equal(
            [
                $"OverviewOpenBills {Money("90.00")} [en]",
                $"OverviewOverdueBills {Money("30.00")} [en]",
                $"OverviewPrepayments {Money("22.00")} [en]",
                $"ContractBalanceDebt {Money("68.00")} [en]",
            ],
            block.Lines.Select(l => l.Text));
        Assert.Equal([OverviewTone.Neutral, OverviewTone.Debt, OverviewTone.Good, OverviewTone.Debt], block.Lines.Select(l => l.Tone));
    }

    [Fact]
    public async Task Money_numbers_equal_the_bills_and_payments_screens_for_the_same_filters()
    {
        AddMoneyScenario();
        var overview = await LoadAsync();
        var lines = overview.MoneyBlocks[0].Lines.Select(l => l.Text).ToList();

        // Bills screen: all years, status Open -> the "open" total; status Overdue -> the overdue total.
        _kit.Filter.Period = PeriodFilter.ForAllYears(2026);
        _kit.Filter.Status = BillStatusFilter.Open;
        var bills = _kit.CreateBills();
        await bills.ReloadAsync();
        var openOnBills = LastPart(bills.TotalsText, 2);
        Assert.Contains($"OverviewOpenBills {openOnBills} [en]", lines);

        _kit.Filter.Status = BillStatusFilter.Overdue;
        bills = _kit.CreateBills();
        await bills.ReloadAsync();
        var overdueOnBills = LastPart(bills.TotalsText, 2);
        Assert.Contains($"OverviewOverdueBills {overdueOnBills} [en]", lines);

        // Payments screen: all years -> the prepayment total.
        _kit.PaymentFilter.Period = PeriodFilter.ForAllYears(2026);
        var payments = _kit.CreatePayments();
        await payments.ReloadAsync();
        var prepaymentOnPayments = LastPart(payments.TotalsText, 1);
        Assert.Contains($"OverviewPrepayments {prepaymentOnPayments} [en]", lines);
    }

    [Fact]
    public async Task Prepaid_contract_shows_paid_in_advance()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        _kit.AddPayment(50m, (bill, 50m));
        var extra = new Payment { ContractId = water.Id, PaidOn = new DateOnly(2026, 10, 25), Amount = 12.30m };
        _kit.Payments.Add(extra);

        var vm = await LoadAsync();

        Assert.Equal(
            [$"OverviewPrepayments {Money("12.30")} [en]", $"ContractBalanceAdvance {Money("12.30")} [en]"],
            vm.MoneyBlocks[0].Lines.Select(l => l.Text));
        Assert.Equal([OverviewTone.Good, OverviewTone.Good], vm.MoneyBlocks[0].Lines.Select(l => l.Tone));
    }

    [Fact]
    public async Task Money_lines_open_bills_filtered_payments_or_the_settlement()
    {
        var (water, _, _, _) = AddMoneyScenario();
        water.BillingMode = BillingMode.Equalized;
        _loader.Settlements[water.Id] = 8.35m;
        var vm = await LoadAsync();
        var lines = vm.MoneyBlocks[0].Lines;
        Assert.Equal($"OverviewSettlementSurcharge Water|{Money("8.35")} [en]", lines[^1].Text);

        foreach (var line in lines)
        {
            line.OpenCommand.Execute(null);
        }

        await Task.Delay(50);

        var visited = _kit.Kit.Navigation.Visited;
        Assert.Equal([Routes.BillsFor, Routes.BillsFor, Routes.PaymentsFor, Routes.BillsFor, Routes.EqualizedSettlement], visited.Select(v => v.Route));
        Assert.Equal(["Open", "Overdue", null, "All"], visited.Take(4).Select(v => v.Parameters!.GetValueOrDefault(BillsViewModel.StatusParameter)));
        Assert.All(visited.Take(4), v => Assert.Equal(_kit.Kit.Property.Id, v.Parameters![EditorNavigation.PropertyIdParameter]));
        Assert.Equal(water.Id, visited[4].Parameters![EditorNavigation.ContractIdParameter]);
    }

    [Fact]
    public async Task This_year_card_compares_with_the_same_months_of_last_year()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 3), 1000m, due: null);
        _kit.AddBill(water, new YearMonth(2026, 10), 245.30m, due: null);
        _kit.AddBill(water, new YearMonth(2026, 11), 22.95m, due: null); // next month: not counted yet
        _kit.AddBill(water, new YearMonth(2025, 3), 900m, due: null);
        _kit.AddBill(water, new YearMonth(2025, 10), 280.10m, due: null);
        _kit.AddBill(water, new YearMonth(2025, 12), 700m, due: null); // after October: not the same period
        var vm = await LoadAsync();

        var block = Assert.Single(vm.YearBlocks);
        var line = Assert.Single(block.Lines);
        Assert.Equal($"OverviewYearLine 2026|{Money("1,245.30")}|2025|{Money("1,180.10")}|+5.5 % [en]", line.Text);

        line.OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillsFor, route);
        Assert.Equal(2026, parameters![BillsViewModel.YearParameter]);
        Assert.Equal("All", parameters[BillsViewModel.StatusParameter]);
    }

    [Fact]
    public async Task This_year_without_last_year_has_no_percentage()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 3), 100m, due: null);

        var vm = await LoadAsync();

        Assert.Equal($"OverviewYearLineNoChange 2026|{Money("100.00")}|2025|{Money("0.00")} [en]", vm.YearBlocks[0].Lines[0].Text);
    }

    // ---- Filter, layout, refresh, help ---------------------------------------------------------------------------

    private Property AddCottage()
    {
        var cottage = new Property { Name = "Cottage" };
        _kit.Kit.Properties.Stored.Add(cottage);
        var power = new Contract { PropertyId = cottage.Id, Title = "Power", ValidFrom = new DateOnly(2020, 1, 1) };
        _kit.Kit.Contracts.Stored.Add(power);
        var bill = Bill.CreateFor(power, new YearMonth(2026, 9), _kit.Kit.Clock);
        bill.Gross = 70m;
        bill.DueOn = new DateOnly(2026, 10, 1);
        _kit.Bills.All.Add(bill);
        return cottage;
    }

    [Fact]
    public async Task The_property_filter_limits_all_cards_and_is_remembered()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var cottage = AddCottage();
        var vm = await LoadAsync();
        Assert.Equal(2, vm.TodoRows.Count);
        Assert.Equal(2, vm.MoneyBlocks.Count);
        Assert.True(vm.ShowPropertyFilter);
        Assert.Equal("BillAllProperties [en]", vm.PropertyFilterName);

        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Cottage") ? "Cottage" : null;
        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);

        Assert.Equal("Cottage", vm.PropertyFilterName);
        Assert.Equal(["TodoBillDue Power|09.2026 [en]"], vm.TodoRows.Select(r => r.Text));
        Assert.Equal(["Cottage"], vm.MoneyBlocks.Select(b => b.Header));
        Assert.Equal(["Cottage"], vm.YearBlocks.Select(b => b.Header));
        Assert.Equal(cottage.Id.ToString(), _kit.Kit.Settings.Values[OverviewViewModel.PropertyFilterKey]);

        // A new view model (the screen is recreated) starts with the remembered filter.
        var again = await LoadAsync();
        Assert.Equal(cottage.Id, again.PropertyFilterId);
        Assert.Single(again.TodoRows);
    }

    [Fact]
    public async Task Choosing_all_properties_clears_the_filter_and_a_vanished_property_resets_it()
    {
        _kit.AddWaterContract(tariff: false);
        var cottage = AddCottage();
        _kit.Kit.Settings.Values[OverviewViewModel.PropertyFilterKey] = cottage.Id.ToString();
        var vm = await LoadAsync();
        Assert.Equal(cottage.Id, vm.PropertyFilterId);

        _kit.Kit.Dialogs.ChooseScript = options => options[0];
        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);
        Assert.Null(vm.PropertyFilterId);
        Assert.False(_kit.Kit.Settings.Values.ContainsKey(OverviewViewModel.PropertyFilterKey));

        _kit.Kit.Settings.Values[OverviewViewModel.PropertyFilterKey] = Guid.NewGuid().ToString();
        var other = await LoadAsync();
        Assert.Null(other.PropertyFilterId);
    }

    [Fact]
    public async Task The_property_filter_is_hidden_with_one_property()
    {
        _kit.AddWaterContract(tariff: false);

        var vm = await LoadAsync();

        Assert.False(vm.ShowPropertyFilter);
    }

    [Fact]
    public async Task Wide_layouts_show_two_columns_and_phones_one()
    {
        var vm = await LoadAsync();
        Assert.True(vm.IsCompact);
        Assert.False(vm.IsWide);

        _kit.Kit.Layout.Change(LayoutMode.Expanded);
        Assert.True(vm.IsWide);
        Assert.False(vm.IsCompact);

        _kit.Kit.Layout.Change(LayoutMode.Compact);
        Assert.True(vm.IsCompact);
    }

    [Fact]
    public async Task Pull_to_refresh_reloads_and_stops_refreshing()
    {
        var vm = await LoadAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, _loader.Loads);
        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public async Task A_language_change_rebuilds_the_rows_without_loading_again()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var vm = await LoadAsync();

        _kit.Kit.Language.SetLanguage("ru");

        Assert.Equal("TodoBillDue Water|09.2026 [ru]", vm.TodoRows[0].Text);
        Assert.Equal(1, _loader.Loads);
    }

    [Fact]
    public async Task Help_opens_the_overview_section_of_the_guide()
    {
        var vm = Create();

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.HelpSection, route);
        Assert.Equal(HelpTopic.Overview, parameters![HelpNavigation.TopicParameter]);
    }

    // ---- Fakes ---------------------------------------------------------------------------------------------------

    private sealed class KitOverviewLoader(ContractTestKit kit) : IOverviewDataLoader
    {
        public int Loads { get; private set; }

        public Dictionary<Guid, decimal> Settlements { get; } = [];

        public Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default)
        {
            Loads++;
            return Task.FromResult(new OverviewData(
                [.. kit.Properties.Stored.Where(p => !p.IsArchived)],
                [.. kit.Contracts.Stored],
                [.. kit.Contracts.Links],
                [.. kit.Meters.Stored],
                [.. kit.Bills.Stored.Select(Copies.Of)],
                [.. kit.Payments.Stored.Select(Copies.Of)],
                new Dictionary<Guid, decimal>(Settlements)));
        }
    }

    private sealed class FakeInitializer : IDatabaseInitializer
    {
        public int Calls { get; private set; }

        public Exception? Failure { get; set; }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            Calls++;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
