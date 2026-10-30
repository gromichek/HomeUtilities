using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The bills list: filters, totals, badges, difference colours and the Paid quick action. Today is 27 Oct 2026.</summary>
public class BillsViewModelTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public BillsViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private async Task<BillsViewModel> LoadAsync()
    {
        var vm = _kit.CreateBills();
        await vm.ReloadAsync();
        return vm;
    }

    /// <summary>The usual list: water Aug (paid), Sep (partially paid, overdue), Oct (open, due later) and internet in advance for Nov.</summary>
    private (Contract Water, Contract Internet, Bill Aug, Bill Sep, Bill Oct, Bill Nov) AddTypicalBills()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var internet = _kit.AddWaterContract("Internet", tariff: false, timing: BillingTiming.InAdvance, dueDay: 15);
        var aug = _kit.AddBill(water, new YearMonth(2026, 8), 40m, due: new DateOnly(2026, 9, 20));
        var sep = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var oct = _kit.AddBill(water, new YearMonth(2026, 10), 60m, due: new DateOnly(2026, 11, 20));
        var nov = _kit.AddBill(internet, new YearMonth(2026, 11), 22m, due: new DateOnly(2026, 10, 15));
        _kit.AddPayment(40m, (aug, 40m));
        _kit.AddPayment(20m, (sep, 20m));
        _kit.AddPayment(22m, (nov, 22m));
        return (water, internet, aug, sep, oct, nov);
    }

    [Fact]
    public async Task List_ShowsTheBillsOfTheCurrentYear_NewestPeriodFirst_WithBadges()
    {
        var (_, _, aug, sep, oct, nov) = AddTypicalBills();

        var vm = await LoadAsync();

        Assert.Equal([nov.Id, oct.Id, sep.Id, aug.Id], vm.Items.Select(i => i.Id));
        Assert.Equal(
            [BillBadgeKind.Prepaid, BillBadgeKind.Unpaid, BillBadgeKind.Overdue, BillBadgeKind.Paid],
            vm.Items.Select(i => i.Badge));
        Assert.Equal(
            ["BillBadgePrepaid [en]", "BillBadgeUnpaid [en]", $"BillBadgeOverdueOpen 30.00 {Eur} [en]", "BillBadgePaid [en]"],
            vm.Items.Select(i => i.BadgeText));
        Assert.Equal(["11.2026", "10.2026", "09.2026", "08.2026"], vm.Items.Select(i => i.PeriodText));
        Assert.Equal(["Internet", "Water", "Water", "Water"], vm.Items.Select(i => i.Title));
        Assert.Equal($"60.00 {Eur}", vm.Items[1].TotalText);
        Assert.Equal("Water utility Ltd", vm.Items[1].SupplierName);
    }

    [Fact]
    public async Task PartiallyPaidBill_NotYetDue_ShowsTheOpenAmount()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var oct = _kit.AddBill(water, new YearMonth(2026, 10), 60m, due: new DateOnly(2026, 11, 20));
        _kit.AddPayment(25m, (oct, 25m));

        var vm = await LoadAsync();

        var item = Assert.Single(vm.Items);
        Assert.Equal(BillBadgeKind.PartiallyPaid, item.Badge);
        Assert.Equal($"BillBadgePartial 35.00 {Eur} [en]", item.BadgeText);
        Assert.True(item.CanMarkPaid);
        Assert.True(item.IsBadgePartial);
    }

    [Fact]
    public async Task Totals_AreBilledPaidAndOpenOfTheFilteredList()
    {
        AddTypicalBills();

        var vm = await LoadAsync();

        // billed 40 + 50 + 60 + 22 = 172; paid 40 + 20 + 0 + 22 = 82; open 90
        Assert.Equal($"BillTotals 172.00 {Eur}|82.00 {Eur}|90.00 {Eur} [en]", vm.TotalsText);
    }

    [Fact]
    public async Task StatusFilter_OpenOverduePaid()
    {
        var (_, _, aug, sep, oct, nov) = AddTypicalBills();
        var vm = await LoadAsync();

        _kit.Kit.Dialogs.ChooseScript = o => o.First(x => x.Contains("BillStatusOpen", StringComparison.Ordinal));
        await vm.ChooseStatusFilterCommand.ExecuteAsync(null);
        Assert.Equal([oct.Id, sep.Id], vm.Items.Select(i => i.Id));
        Assert.Equal($"BillTotals 110.00 {Eur}|20.00 {Eur}|90.00 {Eur} [en]", vm.TotalsText);

        _kit.Kit.Dialogs.ChooseScript = o => o.First(x => x.Contains("BillStatusOverdue", StringComparison.Ordinal));
        await vm.ChooseStatusFilterCommand.ExecuteAsync(null);
        Assert.Equal([sep.Id], vm.Items.Select(i => i.Id));

        _kit.Kit.Dialogs.ChooseScript = o => o.First(x => x.Contains("BillStatusPaid", StringComparison.Ordinal));
        await vm.ChooseStatusFilterCommand.ExecuteAsync(null);
        Assert.Equal([nov.Id, aug.Id], vm.Items.Select(i => i.Id));

        _kit.Kit.Dialogs.ChooseScript = o => o.First(x => x.Contains("BillStatusAll", StringComparison.Ordinal));
        await vm.ChooseStatusFilterCommand.ExecuteAsync(null);
        Assert.Equal(4, vm.Items.Count);
    }

    [Fact]
    public async Task YearFilter_ShowsOneYear_ArrowsMoveIt_AndAllYearsShowsEverything()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var old = _kit.AddBill(water, new YearMonth(2025, 12), 30m, due: new DateOnly(2026, 1, 20));
        var current = _kit.AddBill(water, new YearMonth(2026, 1), 31m, due: new DateOnly(2026, 2, 20));
        var vm = await LoadAsync();

        Assert.Equal("2026", vm.Period.Label);
        Assert.Equal([current.Id], vm.Items.Select(i => i.Id));

        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("2025", vm.Period.Label);
        Assert.Equal([old.Id], vm.Items.Select(i => i.Id));

        vm.Period.ShowAllYearsCommand.Execute(null);
        Assert.Equal("PeriodAllYears [en]", vm.Period.Label);
        Assert.False(vm.Period.ShowStepper);
        Assert.Equal([current.Id, old.Id], vm.Items.Select(i => i.Id));

        vm.Period.ShowYearCommand.Execute(null);
        vm.Period.NextCommand.Execute(null);
        Assert.Equal("2026", vm.Period.Label);
        Assert.Equal([current.Id], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task MonthFilter_ShowsTheBillsOfOnePeriod_ArrowsStepByMonth_TotalsFollow()
    {
        var (_, _, aug, sep, oct, _) = AddTypicalBills();
        var vm = await LoadAsync();

        // Year -> Month: today (27 Oct 2026) lies in the selected year -> October 2026.
        vm.Period.ShowMonthCommand.Execute(null);
        Assert.Equal("October 2026", vm.Period.Label);
        Assert.Equal([oct.Id], vm.Items.Select(i => i.Id));
        Assert.Equal($"BillTotals 60.00 {Eur}|0.00 {Eur}|60.00 {Eur} [en]", vm.TotalsText);

        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("September 2026", vm.Period.Label);
        Assert.Equal([sep.Id], vm.Items.Select(i => i.Id));
        Assert.Equal($"BillTotals 50.00 {Eur}|20.00 {Eur}|30.00 {Eur} [en]", vm.TotalsText);

        vm.Period.PreviousCommand.Execute(null);
        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("July 2026", vm.Period.Label);
        Assert.Empty(vm.Items);
        Assert.True(vm.ShowNothingFound);

        vm.Period.NextCommand.Execute(null);
        Assert.Equal([aug.Id], vm.Items.Select(i => i.Id));

        // Month -> Year keeps the year.
        vm.Period.ShowYearCommand.Execute(null);
        Assert.Equal("2026", vm.Period.Label);
        Assert.Equal(4, vm.Items.Count);
    }

    [Fact]
    public async Task MonthFilter_StepsAcrossTheYearBoundary()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var december = _kit.AddBill(water, new YearMonth(2025, 12), 30m);
        var january = _kit.AddBill(water, new YearMonth(2026, 1), 31m);
        var vm = await LoadAsync();
        vm.Period.ShowMonthCommand.Execute(null);

        for (var step = 0; step < 9; step++)
        {
            vm.Period.PreviousCommand.Execute(null);
        }

        Assert.Equal("January 2026", vm.Period.Label);
        Assert.Equal([january.Id], vm.Items.Select(i => i.Id));

        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("December 2025", vm.Period.Label);
        Assert.Equal([december.Id], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task MonthFilter_RespectsTheStatusFilter()
    {
        var (_, _, _, sep, _, _) = AddTypicalBills();
        var vm = await LoadAsync();
        vm.Period.ShowMonthCommand.Execute(null);
        vm.Period.PreviousCommand.Execute(null);
        _kit.Kit.Dialogs.ChooseScript = o => o.First(x => x.Contains("BillStatusOverdue", StringComparison.Ordinal));

        await vm.ChooseStatusFilterCommand.ExecuteAsync(null);

        Assert.Equal([sep.Id], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task PropertyFilter_LimitsTheListToOneProperty()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var flat = _kit.AddBill(water, new YearMonth(2026, 10), 30m);
        var cottage = new Property { Name = "Cottage" };
        _kit.Kit.Properties.Stored.Add(cottage);
        var tax = Bill.CreateOneOff(cottage.Id, _kit.Kit.CategoryIdFor(ServiceKind.ColdWater), "Roof repair", new YearMonth(2026, 9), _kit.Kit.Clock);
        tax.Gross = 500m;
        _kit.Bills.All.Add(tax);
        var vm = await LoadAsync();
        Assert.Equal(2, vm.Items.Count);

        _kit.Kit.Dialogs.ChooseScript = o => "Cottage";
        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);

        Assert.Equal([tax.Id], vm.Items.Select(i => i.Id));
        Assert.Equal("Cottage", vm.PropertyFilterName);
        Assert.Equal("Roof repair", vm.Items[0].Title);

        _kit.Kit.Dialogs.ChooseScript = o => o[0];
        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);
        Assert.Equal(new[] { flat.Id, tax.Id }.Order(), vm.Items.Select(i => i.Id).Order());
        Assert.Equal("BillAllProperties [en]", vm.PropertyFilterName);
    }

    [Fact]
    public async Task Filters_AreRememberedForTheSession_AcrossViewModels()
    {
        AddTypicalBills();
        var first = await LoadAsync();
        _kit.Kit.Dialogs.ChooseScript = o => o.First(x => x.Contains("BillStatusOverdue", StringComparison.Ordinal));
        await first.ChooseStatusFilterCommand.ExecuteAsync(null);
        first.Period.ShowMonthCommand.Execute(null);
        first.Period.PreviousCommand.Execute(null);

        var second = await LoadAsync();

        Assert.Equal(BillStatusFilter.Overdue, second.StatusFilter);
        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(2026, 9)), second.Period.Current);
        Assert.Equal("September 2026", second.Period.Label);
        Assert.Single(second.Items); // the overdue September bill
    }

    [Theory]
    [InlineData(47.30, false, false)] // +2.20 against 45.10 = +4.9 %: muted
    [InlineData(47.35, false, false)] // +2.25 = +4.99 % -> 5.0: not above +5, muted
    [InlineData(47.40, true, false)] // +2.30 = +5.1 %: red
    [InlineData(42.90, false, false)] // -2.20 = -4.9 %: muted
    [InlineData(42.80, false, true)] // -2.30 = -5.1 %: green
    public async Task Difference_IsRedAboveFivePercent_GreenBelowMinusFive_AndMutedOtherwise(double gross, bool higher, bool lower)
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 10), (decimal)gross, snapshot: _kit.Calculation(water, new YearMonth(2026, 10), 45.10m));

        var vm = await LoadAsync();

        var item = Assert.Single(vm.Items);
        Assert.True(item.HasDifference);
        Assert.Equal((higher, lower), (item.DifferenceHigher, item.DifferenceLower));
    }

    [Fact]
    public async Task Difference_IsShownWithSignMoneyAndPercent()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 10), 46.02m, snapshot: _kit.Calculation(water, new YearMonth(2026, 10), 45.10m));

        var vm = await LoadAsync();

        Assert.Equal($"+0.92 {Eur} (+2.0 %)", Assert.Single(vm.Items).DifferenceText);
    }

    [Fact]
    public async Task BillWithoutSnapshot_HasNoDifference()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 10), 46.02m);

        var vm = await LoadAsync();

        Assert.False(Assert.Single(vm.Items).HasDifference);
    }

    [Fact]
    public async Task Paid_FromTheList_CreatesAQuickPayment_AndShowsUndo()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 46.02m, due: new DateOnly(2026, 11, 20));
        var vm = await LoadAsync();
        Assert.True(vm.Items[0].CanMarkPaid);

        await vm.MarkPaidAsync(bill.Id);

        var payment = Assert.Single(_kit.Payments.Stored);
        Assert.True(payment.CreatedByQuickAction);
        Assert.Equal(46.02m, payment.Amount);
        Assert.Equal(BillBadgeKind.Paid, vm.Items[0].Badge);
        Assert.False(vm.Items[0].CanMarkPaid);
        var call = Assert.Single(_kit.Kit.Toast.Calls);
        Assert.Equal(("BillMarkedPaid [en]", "BillUndo [en]"), (call.Message, call.Action));
    }

    [Fact]
    public async Task Paid_Undo_BringsTheBillBackToUnpaid()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 46.02m, due: new DateOnly(2026, 11, 20));
        var vm = await LoadAsync();
        _kit.Kit.Toast.ActionResult = (_, _) => true;

        await vm.MarkPaidAsync(bill.Id);

        Assert.Empty(_kit.Payments.Stored);
        Assert.Single(_kit.Payments.All);
        Assert.Equal(BillBadgeKind.Unpaid, vm.Items[0].Badge);
        Assert.True(vm.Items[0].CanMarkPaid);
    }

    [Fact]
    public async Task Paid_TappedTwice_CreatesOnePayment()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 46.02m);
        var vm = await LoadAsync();

        await Task.WhenAll(vm.MarkPaidAsync(bill.Id), vm.MarkPaidAsync(bill.Id));

        Assert.Single(_kit.Payments.Stored);
    }

    [Fact]
    public async Task PaidButton_OfTheRow_CallsTheQuickAction()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 10), 46.02m);
        var vm = await LoadAsync();

        vm.Items[0].MarkPaidCommand.Execute(null);
        await Task.Delay(100);

        Assert.Single(_kit.Payments.Stored);
    }

    [Fact]
    public async Task OpenFor_ShowsAllYearsOfThePropertyAndHighlightsTheContract()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var internet = _kit.AddWaterContract("Internet", tariff: false);
        var old = _kit.AddBill(water, new YearMonth(2024, 5), 30m);
        var other = _kit.AddBill(internet, new YearMonth(2026, 5), 20m);
        var vm = await LoadAsync();

        await vm.OpenForAsync(_kit.Kit.Property.Id, water.Id);

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(PeriodScope.AllYears, vm.Period.Current.Scope);
        Assert.True(vm.Items.Single(i => i.Id == old.Id).IsHighlighted);
        Assert.False(vm.Items.Single(i => i.Id == other.Id).IsHighlighted);
    }

    [Fact]
    public async Task OpenFor_SelectingABill_DropsTheContractHighlightAndSelectsOnlyThatBill()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var first = _kit.AddBill(water, new YearMonth(2024, 5), 30m);
        var second = _kit.AddBill(water, new YearMonth(2024, 6), 31m);
        var vm = await LoadAsync();
        await vm.OpenForAsync(_kit.Kit.Property.Id, water.Id);
        Assert.All(vm.Items, i => Assert.True(i.IsHighlighted));

        await vm.SelectCommand.ExecuteAsync(first.Id);

        Assert.Equal([first.Id], vm.Items.Where(i => i.IsSelected).Select(i => i.Id));
        Assert.All(vm.Items, i => Assert.False(i.IsHighlighted));

        await vm.SelectCommand.ExecuteAsync(second.Id);

        Assert.Equal([second.Id], vm.Items.Where(i => i.IsSelected).Select(i => i.Id));
    }

    [Fact]
    public async Task OpenFor_FromTheOverview_AppliesTheStatusFilter()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var paid = _kit.AddBill(water, new YearMonth(2026, 8), 40m, due: new DateOnly(2026, 9, 20));
        var overdue = _kit.AddBill(water, new YearMonth(2026, 9), 50m, due: new DateOnly(2026, 10, 20));
        var open = _kit.AddBill(water, new YearMonth(2026, 10), 60m, due: new DateOnly(2026, 11, 20));
        _kit.AddPayment(40m, (paid, 40m));
        var vm = await LoadAsync();

        await vm.OpenForAsync(_kit.Kit.Property.Id, null, BillStatusFilter.Overdue);
        Assert.Equal([overdue.Id], vm.Items.Select(i => i.Id));

        await vm.OpenForAsync(_kit.Kit.Property.Id, null, BillStatusFilter.Open);
        Assert.Equal([open.Id, overdue.Id], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task OpenFor_FromTheOverview_CanShowOneYear()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var last = _kit.AddBill(water, new YearMonth(2025, 5), 30m);
        var current = _kit.AddBill(water, new YearMonth(2026, 5), 20m);
        var vm = await LoadAsync();

        await vm.OpenForAsync(_kit.Kit.Property.Id, null, BillStatusFilter.All, 2025);

        Assert.Equal((PeriodScope.Year, 2025), (vm.Period.Current.Scope, vm.Period.Current.Year));
        Assert.Equal([last.Id], vm.Items.Select(i => i.Id));
        Assert.NotEqual(current.Id, last.Id);
    }

    /// <summary>Water (built-in category, default colour) and a user category "Kindergarten" (#112233), one bill each in October 2026.</summary>
    private (Bill Water, Bill Kindergarten, Category Category) AddTwoCategories()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var kindergarten = _kit.AddWaterContract("Kindergarten", tariff: false);
        var category = new Category { Name = "Kindergarten", Color = "#112233", SortOrder = 99 };
        _kit.Kit.Categories.Stored.Add(category);
        kindergarten.CategoryId = category.Id;
        var waterBill = _kit.AddBill(water, new YearMonth(2026, 10), 60m);
        var otherBill = _kit.AddBill(kindergarten, new YearMonth(2026, 10), 90m);
        return (waterBill, otherBill, category);
    }

    [Fact]
    public async Task CategoryFilter_ShowsOnlyTheBillsOfTheCategory_AndTheTotalsFollow()
    {
        var (water, kindergarten, category) = AddTwoCategories();
        var vm = await LoadAsync();
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("BillAllCategories [en]", vm.CategoryFilterName);
        Assert.False(vm.HasCategoryFilterColor);

        _kit.Kit.Dialogs.ChooseScript = options => options.Single(o => o == "Kindergarten");
        await vm.ChooseCategoryFilterCommand.ExecuteAsync(null);

        Assert.Equal([kindergarten.Id], vm.Items.Select(i => i.Id));
        Assert.Equal(category.Id, vm.CategoryFilterId);
        Assert.Equal("Kindergarten", vm.CategoryFilterName);
        Assert.Equal("#112233", vm.CategoryFilterColor);
        Assert.True(vm.HasCategoryFilterColor);
        Assert.Contains("90.00", vm.TotalsText);
        Assert.DoesNotContain("150.00", vm.TotalsText);

        _kit.Kit.Dialogs.ChooseScript = options => options[0];
        await vm.ChooseCategoryFilterCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.Null(vm.CategoryFilterId);
        Assert.NotEqual(water.Id, kindergarten.Id);
    }

    [Fact]
    public async Task CategoryFilter_OffersAllAndTheCategories_AndCancelKeepsTheFilter()
    {
        AddTwoCategories();
        var vm = await LoadAsync();
        IReadOnlyList<string>? offered = null;
        _kit.Kit.Dialogs.ChooseScript = options =>
        {
            offered = options;
            return null;
        };

        await vm.ChooseCategoryFilterCommand.ExecuteAsync(null);

        Assert.NotNull(offered);
        Assert.Equal("BillAllCategories [en]", offered[0]);
        Assert.Contains("Kindergarten", offered);
        Assert.Null(vm.CategoryFilterId);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public async Task CategoryFilter_IsRememberedWithTheOtherFilters()
    {
        var (_, kindergarten, _) = AddTwoCategories();
        var vm = await LoadAsync();
        _kit.Kit.Dialogs.ChooseScript = options => "Kindergarten";
        await vm.ChooseCategoryFilterCommand.ExecuteAsync(null);

        var again = await LoadAsync();

        Assert.Equal([kindergarten.Id], again.Items.Select(i => i.Id));
        Assert.Equal("Kindergarten", again.CategoryFilterName);
    }

    [Fact]
    public async Task OpenFor_WithACategory_FiltersByPropertyMonthAndCategory()
    {
        var (water, kindergarten, category) = AddTwoCategories();
        var earlier = _kit.AddBill(_kit.Kit.Contracts.Stored.Single(c => c.Title == "Kindergarten"), new YearMonth(2026, 9), 80m);
        var vm = await LoadAsync();

        await vm.OpenForAsync(_kit.Kit.Property.Id, null, month: new YearMonth(2026, 10), categoryId: category.Id);

        Assert.Equal([kindergarten.Id], vm.Items.Select(i => i.Id));
        Assert.Equal(category.Id, vm.CategoryFilterId);
        Assert.NotEqual(earlier.Id, water.Id);

        // Opened again without a category (from the Overview): the category filter is cleared.
        await vm.OpenForAsync(_kit.Kit.Property.Id, null, month: new YearMonth(2026, 10));
        Assert.Equal(2, vm.Items.Count);
        Assert.Null(vm.CategoryFilterId);
    }

    [Fact]
    public async Task EmptyList_ShowsTheEmptyState()
    {
        var vm = await LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Items);
        Assert.False(vm.ShowNothingFound);
    }

    [Fact]
    public async Task TappingARow_OpensTheEditorPage_OnThePhone()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 46.02m);
        var vm = await LoadAsync();

        await vm.SelectCommand.ExecuteAsync(bill.Id);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillEdit, route);
        var editor = Assert.IsType<BillEditViewModel>(parameters![EditorNavigation.EditorParameter]);
        Assert.Equal(bill.Id, editor.EntityId);
        Assert.False(editor.IsNew);
    }

    [Fact]
    public async Task Editor_InThePane_SavingRefreshesTheList()
    {
        _kit.Kit.Layout.Change(LayoutMode.Expanded);
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 46.02m);
        var vm = _kit.CreateBills();
        await vm.OnAppearingAsync();

        await vm.SelectCommand.ExecuteAsync(bill.Id);
        var editor = vm.SelectedEditor!;
        editor.Amounts.GrossText = "50";
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal($"50.00 {Eur}", vm.Items[0].TotalText);
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task HelpButton_OfTheListAndOfTheEditor_OpensTheBillsSection()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(water, new YearMonth(2026, 10), 46.02m);
        var list = await LoadAsync();
        var editor = _kit.CreateEditor();
        await editor.LoadAsync(bill.Id);

        await list.OpenHelpCommand.ExecuteAsync(null);
        await editor.OpenHelpCommand.ExecuteAsync(null);

        Assert.All(_kit.Kit.Navigation.Visited, visit =>
        {
            Assert.Equal(Routes.HelpSection, visit.Route);
            Assert.Equal(HelpTopic.Bills, visit.Parameters![HelpNavigation.TopicParameter]);
        });
        Assert.Equal(2, _kit.Kit.Navigation.Visited.Count);
    }
}
