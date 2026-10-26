using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The Payments screen over the flat of <see cref="BillTestKit"/> and a second property. Today is 3 Nov 2026.</summary>
public class PaymentsViewModelTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 11, 3);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);
    private readonly Contract _water;
    private readonly Contract _waste;
    private readonly Property _cottage = new() { Name = "Cottage", Kind = PropertyKind.House };
    private readonly Payment _transfer;
    private readonly Payment _prepaid;
    private readonly Payment _refund;
    private readonly Payment _lastYear;
    private readonly Payment _cottagePayment;

    public PaymentsViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        _kit.Kit.Properties.Stored.Add(_cottage);
        _water = _kit.AddWaterContract("Water", tariff: false);
        _waste = new Contract
        {
            PropertyId = _kit.Kit.Property.Id,
            SupplierId = _kit.Supplier.Id,
            Title = "Waste",
            CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Waste),
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Kit.Contracts.Stored.Add(_waste);
        var gardenContract = new Contract
        {
            PropertyId = _cottage.Id,
            SupplierId = _kit.Supplier.Id,
            Title = "Garden power",
            CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Electricity),
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Kit.Contracts.Stored.Add(gardenContract);

        var waterBill = _kit.AddBill(_water, new YearMonth(2026, 9), 30m);
        var wasteBill = _kit.AddBill(_waste, new YearMonth(2026, 9), 28.32m);
        var waterOctober = _kit.AddBill(_water, new YearMonth(2026, 10), 57.70m);
        var correction = _kit.AddBill(_water, new YearMonth(2026, 8), -12m);
        correction.Kind = BillKind.Correction;

        _transfer = Pay(58.32m, new DateOnly(2026, 10, 20), null, (waterBill, 30m), (wasteBill, 28.32m));
        _prepaid = Pay(70m, new DateOnly(2026, 10, 5), _water.Id, (waterOctober, 57.70m));
        _refund = Pay(-12m, new DateOnly(2026, 9, 15), _water.Id, (correction, -12m));
        _lastYear = Pay(20m, new DateOnly(2025, 12, 1), _water.Id);
        _cottagePayment = Pay(15m, new DateOnly(2026, 10, 1), gardenContract.Id);
        _cottagePayment.PropertyId = _cottage.Id;
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static string Money(string amount) => $"{amount} {Eur}";

    private Payment Pay(decimal amount, DateOnly paidOn, Guid? contractId, params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment { Amount = amount, PaidOn = paidOn, ContractId = contractId, PropertyId = _kit.Kit.Property.Id };
        foreach (var (bill, allocated) in allocations)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = allocated });
        }

        _kit.Payments.Add(payment);
        return payment;
    }

    private async Task<PaymentsViewModel> LoadAsync()
    {
        var vm = _kit.CreatePayments();
        await vm.ReloadAsync();
        return vm;
    }

    [Fact]
    public async Task ShowsThePaymentsOfTheCurrentYear_NewestFirst()
    {
        var vm = await LoadAsync();

        // 20 Oct (transfer), 5 Oct (prepaid), 1 Oct (cottage), 15 Sep (refund)
        Assert.Equal(new[] { _transfer.Id, _prepaid.Id, _cottagePayment.Id, _refund.Id }, vm.Items.Select(i => i.Id));
        Assert.Equal("2026", vm.Period.Label);
        Assert.DoesNotContain(vm.Items, i => i.Id == _lastYear.Id);
    }

    [Fact]
    public async Task Row_OfAPaymentForTwoBillsOfDifferentContracts_SaysSeveralContracts()
    {
        var vm = await LoadAsync();
        var row = vm.Items.Single(i => i.Id == _transfer.Id);

        Assert.Equal("20.10.2026", row.DateText);
        Assert.Equal(Money("58.32"), row.AmountText);
        Assert.Equal("PaymentSeveralContracts [en]", row.TitleText);
        Assert.Equal("Water 09.2026, Waste 09.2026", row.PaysText);
        Assert.Equal("Enum_PaymentMethod_BankTransfer [en]", row.MethodText);
        Assert.False(row.HasPrepayment);
        Assert.False(row.IsQuick);
    }

    [Fact]
    public async Task Row_WithUnallocatedMoney_ShowsThePrepaymentBadge()
    {
        var vm = await LoadAsync();
        var row = vm.Items.Single(i => i.Id == _prepaid.Id);

        // 70.00 - 57.70 = 12.30
        Assert.Equal("Water", row.TitleText);
        Assert.Equal($"PaymentPrepaymentBadge {Money("12.30")} [en]", row.PrepaymentText);
        Assert.True(row.HasPrepayment);
    }

    [Fact]
    public async Task Row_OfARefund_IsShownNegativeWithTheRefundMarker()
    {
        var vm = await LoadAsync();
        var row = vm.Items.Single(i => i.Id == _refund.Id);

        Assert.True(row.IsRefund);
        Assert.Equal($"PaymentRefundAmount {Money("12.00")} [en]", row.AmountText);
        Assert.Equal("Water 08.2026", row.PaysText);
        Assert.False(row.HasPrepayment);
    }

    [Fact]
    public async Task Row_NamesTwoBillsAndThenPlusN()
    {
        var contract = _water;
        var bills = Enumerable.Range(1, 4).Select(month => _kit.AddBill(contract, new YearMonth(2025, month), 10m)).ToArray();
        var many = Pay(40m, new DateOnly(2026, 10, 25), contract.Id, bills.Select(b => (b, 10m)).ToArray());

        var vm = await LoadAsync();
        var row = vm.Items.Single(i => i.Id == many.Id);

        // the first two by period, then "+2"
        Assert.Equal("PaymentPaysMore Water 01.2025, Water 02.2025|2 [en]", row.PaysText);
    }

    [Fact]
    public async Task Row_OfAQuickPayment_HasTheQuickMarker()
    {
        var bill = _kit.AddBill(_waste, new YearMonth(2026, 10), 14m);
        var quick = await _kit.QuickPay.MarkPaidAsync(bill.Id);

        var vm = await LoadAsync();

        Assert.Equal("PaymentQuickMarker [en]", vm.Items.Single(i => i.Id == quick!.Id).QuickText);
        Assert.True(vm.Items.Single(i => i.Id == quick!.Id).IsQuick);
    }

    [Fact]
    public async Task Totals_ArePaidAndTheUnallocatedPart()
    {
        var vm = await LoadAsync();

        // 58.32 + 70.00 + 15.00 - 12.00 = 131.32 paid; unallocated 12.30 + 15.00 = 27.30
        Assert.Equal($"PaymentsTotals {Money("131.32")}|{Money("27.30")} [en]", vm.TotalsText);
    }

    [Fact]
    public async Task YearFilter_MovesAndShowsAllYears()
    {
        var vm = await LoadAsync();

        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("2025", vm.Period.Label);
        Assert.Equal([_lastYear.Id], vm.Items.Select(i => i.Id));

        vm.Period.ShowAllYearsCommand.Execute(null);
        Assert.Equal("PeriodAllYears [en]", vm.Period.Label);
        Assert.Equal(5, vm.Items.Count);

        vm.Period.ShowYearCommand.Execute(null);
        vm.Period.NextCommand.Execute(null);
        Assert.Equal("2026", vm.Period.Label);
        Assert.Equal(4, vm.Items.Count);
    }

    [Fact]
    public async Task MonthFilter_ShowsOneCalendarMonthOfPaidOn_ArrowsStep_TotalsFollow()
    {
        var vm = await LoadAsync();

        // Year -> Month: today is 3 Nov 2026 -> November 2026 (no payments yet).
        vm.Period.ShowMonthCommand.Execute(null);
        Assert.Equal("November 2026", vm.Period.Label);
        Assert.Empty(vm.Items);
        Assert.True(vm.ShowNothingFound);

        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("October 2026", vm.Period.Label);
        Assert.Equal([_transfer.Id, _prepaid.Id, _cottagePayment.Id], vm.Items.Select(i => i.Id));
        // 58.32 + 70.00 + 15.00 = 143.32 paid; unallocated 12.30 + 15.00 = 27.30
        Assert.Equal($"PaymentsTotals {Money("143.32")}|{Money("27.30")} [en]", vm.TotalsText);

        vm.Period.PreviousCommand.Execute(null);
        Assert.Equal("September 2026", vm.Period.Label);
        Assert.Equal([_refund.Id], vm.Items.Select(i => i.Id));

        // Month -> Year keeps the year.
        vm.Period.ShowYearCommand.Execute(null);
        Assert.Equal("2026", vm.Period.Label);
        Assert.Equal(4, vm.Items.Count);
    }

    [Fact]
    public async Task MonthFilter_StepsAcrossTheYearBoundary()
    {
        var vm = await LoadAsync();
        vm.Period.ShowMonthCommand.Execute(null);

        for (var step = 0; step < 11; step++)
        {
            vm.Period.PreviousCommand.Execute(null);
        }

        Assert.Equal("December 2025", vm.Period.Label);
        Assert.Equal([_lastYear.Id], vm.Items.Select(i => i.Id));

        vm.Period.NextCommand.Execute(null);
        Assert.Equal("January 2026", vm.Period.Label);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task MonthFilter_IsRememberedByTheNextScreenInstance()
    {
        var vm = await LoadAsync();
        vm.Period.ShowMonthCommand.Execute(null);
        vm.Period.PreviousCommand.Execute(null);

        var again = await LoadAsync();

        Assert.Equal("October 2026", again.Period.Label);
        Assert.Equal(3, again.Items.Count);
    }

    [Fact]
    public async Task PropertyFilter_KeepsThePaymentsOfThatProperty()
    {
        var vm = await LoadAsync();
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Cottage") ? "Cottage" : null;

        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);

        Assert.Equal([_cottagePayment.Id], vm.Items.Select(i => i.Id));
        Assert.Equal("Cottage", vm.PropertyFilterName);
    }

    [Fact]
    public async Task PropertyFilter_AlsoFindsAPaymentWithoutContractBySeeingItsBills()
    {
        _transfer.PropertyId = null; // the bills tell the property
        var vm = await LoadAsync();
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Sample flat") ? "Sample flat" : null;

        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);

        Assert.Contains(vm.Items, i => i.Id == _transfer.Id);
        Assert.DoesNotContain(vm.Items, i => i.Id == _cottagePayment.Id);
    }

    [Fact]
    public async Task ContractFilter_OffersTheContractsOfTheChosenProperty_AndKeepsItsPayments()
    {
        var vm = await LoadAsync();
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Sample flat") ? "Sample flat" : null;
        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);
        IReadOnlyList<string> offered = [];
        _kit.Kit.Dialogs.ChooseScript = options =>
        {
            offered = options;
            return "Water";
        };

        await vm.ChooseContractFilterCommand.ExecuteAsync(null);

        Assert.Equal(["PaymentsAllContracts [en]", "Waste", "Water"], offered);
        // the transfer pays a water bill too
        Assert.Equal([_transfer.Id, _prepaid.Id, _refund.Id], vm.Items.Select(i => i.Id));
        Assert.Equal("Water", vm.ContractFilterName);
    }

    [Fact]
    public async Task ContractFilter_ResetsWhenThePropertyChanges()
    {
        var vm = await LoadAsync();
        _kit.Kit.Dialogs.ChooseScript = _ => "Water";
        await vm.ChooseContractFilterCommand.ExecuteAsync(null);
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Cottage") ? "Cottage" : null;

        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);

        Assert.Null(vm.ContractFilterId);
        Assert.Equal([_cottagePayment.Id], vm.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task OpenFor_ShowsAllYearsOfTheContract_AndHighlightsItsPayments()
    {
        var vm = await LoadAsync();

        await vm.OpenForAsync(_kit.Kit.Property.Id, _water.Id);

        Assert.Equal(PeriodScope.AllYears, vm.Period.Current.Scope);
        Assert.Equal(_water.Id, vm.ContractFilterId);
        Assert.Equal([_transfer.Id, _prepaid.Id, _refund.Id, _lastYear.Id], vm.Items.Select(i => i.Id));
        Assert.All(vm.Items, i => Assert.True(i.IsHighlighted));
    }

    [Fact]
    public async Task Filters_AreRememberedByTheNextScreenInstance()
    {
        var vm = await LoadAsync();
        vm.Period.PreviousCommand.Execute(null);

        var again = await LoadAsync();

        Assert.Equal("2025", again.Period.Label);
    }

    [Fact]
    public async Task Empty_ShowsTheEmptyState_NothingFoundShowsTheHint()
    {
        var empty = new BillTestKit(Today);
        var vm = empty.CreatePayments();
        await vm.ReloadAsync();
        Assert.True(vm.IsEmpty);

        var filtered = await LoadAsync();
        filtered.Period.PreviousCommand.Execute(null);
        filtered.Period.PreviousCommand.Execute(null);
        Assert.False(filtered.IsEmpty);
        Assert.True(filtered.ShowNothingFound);
    }

    [Fact]
    public async Task SelectingARow_OpensTheEditorInTheSidePane()
    {
        _kit.Kit.Layout.Change(LayoutMode.Expanded);
        var vm = _kit.CreatePayments();
        await vm.ReloadAsync();

        await vm.SelectCommand.ExecuteAsync(_prepaid.Id);

        Assert.NotNull(vm.SelectedEditor);
        Assert.Equal(_prepaid.Id, vm.SelectedEditor.EntityId);
        Assert.True(vm.Items.Single(i => i.Id == _prepaid.Id).IsSelected);
    }

    [Fact]
    public async Task Add_InCompactLayout_PushesTheEditorPage()
    {
        var vm = _kit.CreatePayments();
        await vm.ReloadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(Routes.PaymentEdit, Assert.Single(_kit.Kit.Navigation.Visited).Route);
    }
}
