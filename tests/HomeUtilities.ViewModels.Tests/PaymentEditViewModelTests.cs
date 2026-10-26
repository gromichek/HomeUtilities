using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// The payment editor. FakeLocalizer renders texts as "key args [en]"; the kit's flat has a water contract (customer number
/// C-100, purpose "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}").
/// </summary>
public class PaymentEditViewModelTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 11, 3);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);
    private readonly Contract _water;

    public PaymentEditViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        _water = _kit.AddWaterContract("Water", tariff: false);
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static string Money(string amount) => $"{amount} {Eur}";

    private Bill WaterBill(decimal gross, int month, string? number = null)
    {
        var bill = _kit.AddBill(_water, new YearMonth(2026, month), gross, due: new DateOnly(2026, month, 20));
        bill.Number = number;
        return bill;
    }

    /// <summary>A second contract of the same flat.</summary>
    private Contract AddContract(string title)
    {
        var contract = new Contract
        {
            PropertyId = _kit.Kit.Property.Id,
            SupplierId = _kit.Supplier.Id,
            Title = title,
            CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Waste),
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Kit.Contracts.Stored.Add(contract);
        return contract;
    }

    private Bill AddBillOf(Contract contract, int month, decimal gross)
    {
        var bill = _kit.AddBill(contract, new YearMonth(2026, month), gross, due: new DateOnly(2026, month, 20));
        return bill;
    }

    private async Task<PaymentEditViewModel> NewEditorAsync(Guid? billId = null, Guid? contractId = null)
    {
        var vm = _kit.CreatePaymentEditor();
        vm.PresetBillId = billId;
        vm.PresetContractId = contractId;
        await vm.LoadAsync(null);
        return vm;
    }

    private async Task<PaymentEditViewModel> OpenAsync(Guid paymentId)
    {
        var vm = _kit.CreatePaymentEditor();
        await vm.LoadAsync(paymentId);
        return vm;
    }

    private Payment StoredPayment(decimal amount, Guid? contractId, params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment
        {
            ContractId = contractId,
            PropertyId = _kit.Kit.Property.Id,
            PaidOn = Today.AddDays(-2),
            Amount = amount,
        };
        foreach (var (bill, allocated) in allocations)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = allocated });
        }

        _kit.Payments.Add(payment);
        return payment;
    }

    private async Task<BillState> StateOf(Bill bill) =>
        BillStatus.Of(bill, await _kit.Payments.ListByBillAsync(bill.Id), Today);

    /// <summary>Lets the user pick the option that starts with <paramref name="prefix"/> and remembers what was offered.</summary>
    private List<string> Offered(string prefix)
    {
        var offered = new List<string>();
        _kit.Kit.Dialogs.ChooseScript = options =>
        {
            offered.AddRange(options);
            return options.FirstOrDefault(o => o.StartsWith(prefix, StringComparison.Ordinal));
        };
        return offered;
    }

    // ---- Defaults -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task NewPayment_StartsToday_ByBankTransfer_InTheOnlyProperty()
    {
        var vm = await NewEditorAsync();

        Assert.True(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Equal(Today, vm.PaidOn);
        Assert.Equal(PaymentMethod.BankTransfer, vm.SelectedMethod!.As<PaymentMethod>());
        Assert.Equal("Sample flat", vm.SelectedPropertyName);
        Assert.Equal("PaymentNoContract [en]", vm.SelectedContractName);
        Assert.Equal(string.Empty, vm.AmountText);
        Assert.Equal(Eur, vm.CurrencySymbol);
        Assert.False(vm.ShowFillReference);
        Assert.Empty(vm.AllocationRows);
    }

    [Fact]
    public async Task NewPayment_FromAContract_TakesContractAndProperty()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);

        Assert.Equal("Water", vm.SelectedContractName);
        Assert.Equal("Sample flat", vm.SelectedPropertyName);
        Assert.True(vm.ShowFillReference);
        Assert.Empty(vm.AllocationRows);
    }

    [Fact]
    public async Task NewPayment_FromABill_TakesContractTodayOpenAmountAndAllocatesTheBill()
    {
        var bill = WaterBill(46.02m, 10, "W-77");

        var vm = await NewEditorAsync(billId: bill.Id);

        Assert.Equal("Water", vm.SelectedContractName);
        Assert.Equal(Today, vm.PaidOn);
        Assert.Equal("46.02", vm.AmountText);
        var row = Assert.Single(vm.AllocationRows);
        Assert.Equal(bill.Id, row.BillId);
        Assert.Equal($"PaymentAllocationRow Water|10.2026|{Money("46.02")}|{Money("46.02")} [en]", row.Text);
        Assert.Equal($"PaymentSummaryFull {Money("46.02")} [en]", vm.AllocationSummary);
        // the purpose of the contract with the bill's period
        Assert.Equal("Abon. Nr. C-100, 10.2026", vm.Reference);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task NewPayment_FromAPartlyPaidBill_StartsWithTheOpenAmount()
    {
        var bill = WaterBill(46.02m, 10);
        StoredPayment(20m, _water.Id, (bill, 20m));

        var vm = await NewEditorAsync(billId: bill.Id);

        // 46.02 - 20.00 = 26.02
        Assert.Equal("26.02", vm.AmountText);
        Assert.Equal($"PaymentAllocationRow Water|10.2026|{Money("46.02")}|{Money("26.02")} [en]", Assert.Single(vm.AllocationRows).Text);
    }

    [Fact]
    public async Task NewPayment_FromAFullyPaidBill_HasNoAllocation()
    {
        var bill = WaterBill(46.02m, 10);
        StoredPayment(46.02m, _water.Id, (bill, 46.02m));

        var vm = await NewEditorAsync(billId: bill.Id);

        Assert.Empty(vm.AllocationRows);
        Assert.Equal(string.Empty, vm.AmountText);
    }

    // ---- Open-bill picker -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Picker_ForAContract_OffersOnlyItsOpenBillsOfTheSameCurrencyAndSign()
    {
        var open = WaterBill(46.02m, 9);
        var partly = WaterBill(50m, 10);
        StoredPayment(20m, _water.Id, (partly, 20m));
        var paid = WaterBill(40m, 8);
        StoredPayment(40m, _water.Id, (paid, 40m));
        var credit = WaterBill(-5m, 7);
        var lats = WaterBill(10m, 6);
        lats.Currency = Currency.LVL;
        var other = AddContract("Waste");
        AddBillOf(other, 9, 14m);

        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "58.32";
        var offered = Offered("zzz");
        await vm.AddAllocationCommand.ExecuteAsync(null);

        // the bills of the water contract with something open: September (46.02) and October (50.00 - 20.00 = 30.00)
        Assert.Equal(
            [$"PaymentBillOption Water 09.2026|{Money("46.02")} [en]", $"PaymentBillOption Water 10.2026|{Money("30.00")} [en]"],
            offered);
    }

    [Fact]
    public async Task Picker_ForARefund_OffersOnlyBillsWithANegativeOpenAmount()
    {
        WaterBill(46.02m, 9);
        var correction = WaterBill(-12m, 10);
        correction.Kind = BillKind.Correction;

        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "-12.00";
        var offered = Offered("zzz");
        await vm.AddAllocationCommand.ExecuteAsync(null);

        Assert.Equal([$"PaymentBillOption Water 10.2026|{Money("-12.00")} [en]"], offered);
    }

    [Fact]
    public async Task Picker_WithoutContract_OffersTheOpenBillsOfTheWholeProperty()
    {
        WaterBill(46.02m, 9);
        var waste = AddContract("Waste");
        AddBillOf(waste, 9, 14m);
        var oneOff = Bill.CreateOneOff(_kit.Kit.Property.Id, _kit.Kit.CategoryIdFor(ServiceKind.Maintenance), "Repair", new YearMonth(2026, 9), _kit.Kit.Clock);
        oneOff.Gross = 100m;
        _kit.Bills.All.Add(oneOff);

        var vm = await NewEditorAsync();
        vm.AmountText = "160.00";
        var offered = Offered("zzz");
        await vm.AddAllocationCommand.ExecuteAsync(null);

        Assert.Equal(3, offered.Count);
        Assert.Contains(offered, o => o.Contains("Water 09.2026", StringComparison.Ordinal));
        Assert.Contains(offered, o => o.Contains("Waste 09.2026", StringComparison.Ordinal));
        Assert.Contains(offered, o => o.Contains("Repair 09.2026", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Picker_DoesNotOfferABillThatIsAlreadyListed()
    {
        var first = WaterBill(30m, 9);
        WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: first.Id);
        var offered = Offered("zzz");

        await vm.AddAllocationCommand.ExecuteAsync(null);

        Assert.Equal([$"PaymentBillOption Water 10.2026|{Money("46.02")} [en]"], offered);
    }

    [Fact]
    public async Task Picker_WithoutOpenBills_SaysSo()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "10";

        await vm.AddAllocationCommand.ExecuteAsync(null);

        Assert.Contains(_kit.Kit.Dialogs.Alerts, a => a.Message == "PaymentNoOpenBills [en]");
    }

    // ---- Allocation amounts ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AddingBills_DefaultsToTheSmallerOfTheOpenAmountAndWhatIsLeftOfThePayment()
    {
        var september = WaterBill(30m, 9);
        var october = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "58.32";

        await PickAsync(vm, "PaymentBillOption Water 09.2026");
        await PickAsync(vm, "PaymentBillOption Water 10.2026");

        // 58.32: September takes all of its 30.00, October the 28.32 that are left
        Assert.Equal(
            [$"PaymentAllocationRow Water|09.2026|{Money("30.00")}|{Money("30.00")} [en]", $"PaymentAllocationRow Water|10.2026|{Money("46.02")}|{Money("28.32")} [en]"],
            vm.AllocationRows.Select(r => r.Text));
        Assert.Equal($"PaymentSummaryFull {Money("58.32")} [en]", vm.AllocationSummary);
        Assert.False(vm.IsOverAllocated);
        Assert.Equal([september.Id, october.Id], vm.AllocationRows.Select(r => r.BillId));
    }

    [Fact]
    public async Task AllocatingLessThanThePayment_ShowsThePrepayment()
    {
        WaterBill(57.70m, 10);
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "70.00";

        await PickAsync(vm, "PaymentBillOption Water 10.2026");

        // 70.00 - 57.70 = 12.30
        Assert.Equal($"PaymentSummaryPrepayment {Money("57.70")}|{Money("12.30")} [en]", vm.AllocationSummary);
    }

    [Fact]
    public async Task AddingABillWhenNothingIsLeft_AllocatesItsOpenAmountAndShowsOverAllocation()
    {
        var bill = WaterBill(46.02m, 10);
        var extra = WaterBill(10m, 11);
        var vm = await NewEditorAsync(billId: bill.Id);

        await PickAsync(vm, "PaymentBillOption Water 11.2026");

        // 46.02 + 10.00 against a payment of 46.02
        Assert.Equal($"PaymentSummaryOver {Money("56.02")}|{Money("10.00")} [en]", vm.AllocationSummary);
        Assert.True(vm.IsOverAllocated);
        Assert.Equal(extra.Id, vm.AllocationRows[1].BillId);
    }

    [Fact]
    public async Task AutoAllocate_FillsTheOldestOpenBillsFirst()
    {
        var september = WaterBill(30m, 9);
        var october = WaterBill(46.02m, 10);
        WaterBill(40m, 11);
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "58.32";

        await vm.AutoAllocateCommand.ExecuteAsync(null);

        // 58.32 = 30.00 (September) + 28.32 (October)
        Assert.Equal([september.Id, october.Id], vm.AllocationRows.Select(r => r.BillId));
        Assert.Equal($"PaymentSummaryFull {Money("58.32")} [en]", vm.AllocationSummary);
    }

    [Fact]
    public async Task AutoAllocate_OnlyFillsTheUnallocatedPart()
    {
        var september = WaterBill(30m, 9);
        var october = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: october.Id);
        vm.AmountText = "76.02";

        await vm.AutoAllocateCommand.ExecuteAsync(null);

        // 76.02 - 46.02 already on October = 30.00 for September
        Assert.Equal([october.Id, september.Id], vm.AllocationRows.Select(r => r.BillId));
        Assert.Equal($"PaymentSummaryFull {Money("76.02")} [en]", vm.AllocationSummary);
    }

    [Fact]
    public async Task AutoAllocate_WithoutAmount_AsksForIt()
    {
        WaterBill(30m, 9);
        var vm = await NewEditorAsync(contractId: _water.Id);

        await vm.AutoAllocateCommand.ExecuteAsync(null);

        Assert.Empty(vm.AllocationRows);
        Assert.Contains(_kit.Kit.Dialogs.Alerts, a => a.Message == "PaymentEnterAmountFirst [en]");
    }

    [Fact]
    public async Task EditingAnAllocation_ChangesItsAmount()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        vm.AmountText = "60.00";
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (AllocationAmountEditorViewModel)editor;
            dialog.AmountText = "40.00";
            dialog.SaveCommand.Execute(null);
        };

        await vm.AllocationRows[0].EditCommand.ExecuteAsync(null);

        Assert.Equal($"PaymentAllocationRow Water|10.2026|{Money("46.02")}|{Money("40.00")} [en]", vm.AllocationRows[0].Text);
        Assert.Equal($"PaymentSummaryPrepayment {Money("40.00")}|{Money("20.00")} [en]", vm.AllocationSummary);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task AllocationDialog_RefusesMoreThanTheOpenAmountOfTheBill()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (AllocationAmountEditorViewModel)editor;
            dialog.AmountText = "50.00";
            dialog.SaveCommand.Execute(null);
        };

        await vm.AllocationRows[0].EditCommand.ExecuteAsync(null);

        var dialogVm = (AllocationAmountEditorViewModel)_kit.Kit.Dialogs.LastEditor!;
        Assert.False(dialogVm.Saved);
        Assert.Contains("payment_edit_allocation_exceeds_open", dialogVm.AmountError, StringComparison.Ordinal);
        Assert.Equal($"PaymentAllocationRow Water|10.2026|{Money("46.02")}|{Money("46.02")} [en]", vm.AllocationRows[0].Text);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1e400")]
    [InlineData("99999999999999999999")]
    public async Task AllocationDialog_RefusesNonsenseAmounts(string text)
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (AllocationAmountEditorViewModel)editor;
            dialog.AmountText = text;
            dialog.SaveCommand.Execute(null);
        };

        await vm.AllocationRows[0].EditCommand.ExecuteAsync(null);

        var dialogVm = (AllocationAmountEditorViewModel)_kit.Kit.Dialogs.LastEditor!;
        Assert.False(dialogVm.Saved);
        Assert.NotEmpty(dialogVm.AmountError);
    }

    [Fact]
    public async Task RemovingAnAllocation_AsksAndDropsTheRow()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);

        await vm.AllocationRows[0].RemoveCommand.ExecuteAsync(null);

        Assert.Single(_kit.Kit.Dialogs.Confirmations);
        Assert.Empty(vm.AllocationRows);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task RemovingAnAllocation_CanBeCancelled()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        _kit.Kit.Dialogs.ConfirmResult = false;

        await vm.AllocationRows[0].RemoveCommand.ExecuteAsync(null);

        Assert.Single(vm.AllocationRows);
    }

    // ---- Validation and saving ------------------------------------------------------------------------------------

    [Fact]
    public async Task Save_TwoBillsInOnePayment_BothArePaid_AndDeleteMakesThemOpenAgain()
    {
        var september = WaterBill(30m, 9);
        var october = WaterBill(28.32m, 10);
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "58.32";
        await vm.AutoAllocateCommand.ExecuteAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(vm.Errors);
        var stored = Assert.Single(_kit.Payments.Stored);
        Assert.Equal(58.32m, stored.Amount);
        Assert.Equal(_water.Id, stored.ContractId);
        Assert.Equal(_kit.Kit.Property.Id, stored.PropertyId);
        Assert.Equal([30m, 28.32m], stored.Allocations.Select(a => a.Amount));
        Assert.True((await StateOf(september)).IsPaid);
        Assert.True((await StateOf(october)).IsPaid);

        // delete: the dialog warns that the bills become unpaid again
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Contains(_kit.Kit.Dialogs.Confirmations, c => c.Message == "PaymentDeleteWithBills [en]");
        Assert.Empty(_kit.Payments.Stored);
        Assert.Equal(30m, (await StateOf(september)).Open);
        Assert.Equal(28.32m, (await StateOf(october)).Open);
    }

    [Fact]
    public async Task Save_WithoutContract_StoresThePropertyAndAllocatesBillsOfSeveralContracts()
    {
        var water = WaterBill(40m, 10);
        var waste = AddBillOf(AddContract("Waste"), 10, 18.32m);
        var vm = await NewEditorAsync();
        vm.AmountText = "58.32";
        await vm.AutoAllocateCommand.ExecuteAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        var stored = Assert.Single(_kit.Payments.Stored);
        Assert.Null(stored.ContractId);
        Assert.Equal(_kit.Kit.Property.Id, stored.PropertyId);
        Assert.Equal(2, stored.Allocations.Count);
        Assert.True((await StateOf(water)).IsPaid);
        Assert.True((await StateOf(waste)).IsPaid);
    }

    [Fact]
    public async Task Save_KeepsThePageOpen_AndTellsTheList()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        EditOutcome? outcome = null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };
        vm.IsHostedInPage = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(EditOutcome.Saved, outcome);
        Assert.Equal(0, _kit.Kit.Navigation.BackCount);
        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Contains("Saved [en]", _kit.Kit.Toast.Messages);
        Assert.StartsWith("PaymentEditTitle", vm.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_ThenEditAndSaveAgain_UpdatesTheSamePayment()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        await vm.SaveCommand.ExecuteAsync(null);

        vm.AmountText = "50.00";
        await vm.SaveCommand.ExecuteAsync(null);

        var stored = Assert.Single(_kit.Payments.Stored);
        Assert.Equal(50m, stored.Amount);
        Assert.Equal(46.02m, Assert.Single(stored.Allocations, a => !a.IsDeleted).Amount);
        Assert.Equal(3.98m, PaymentRules.Unallocated(stored));
    }

    [Fact]
    public async Task Save_OverAllocated_ShowsTheExcessAndStoresNothing()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        vm.AmountText = "40.00";

        await vm.SaveCommand.ExecuteAsync(null);

        // 46.02 allocated against 40.00: 6.02 too much
        Assert.True(vm.HasAllocationsError);
        Assert.Contains(Money("6.02"), vm.AllocationsError, StringComparison.Ordinal);
        Assert.Empty(_kit.Payments.Saved);
    }

    [Fact]
    public async Task Save_AllocationAboveTheOpenAmountOfTheBill_IsRefused()
    {
        // Data from another device: two payments each settle the whole bill, so the second one allocates more than is open.
        var bill = WaterBill(46.02m, 10);
        StoredPayment(46.02m, _water.Id, (bill, 46.02m));
        var second = StoredPayment(46.02m, _water.Id, (bill, 46.02m));
        var vm = await OpenAsync(second.Id);
        vm.Notes = "changed";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasAllocationsError);
        Assert.Contains("payment_edit_allocation_exceeds_open", vm.AllocationsError, StringComparison.Ordinal);
        Assert.Empty(_kit.Payments.Saved);
    }

    [Fact]
    public async Task Picker_CountsWhatOtherPaymentsAlreadyPaid()
    {
        var bill = WaterBill(46.02m, 10);
        StoredPayment(40m, _water.Id, (bill, 40m));
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "20.00";

        await PickAsync(vm, "PaymentBillOption Water 10.2026");

        // only 6.02 of the bill is open, although 20.00 are available
        Assert.Equal($"PaymentAllocationRow Water|10.2026|{Money("46.02")}|{Money("6.02")} [en]", Assert.Single(vm.AllocationRows).Text);
        Assert.Equal($"PaymentSummaryPrepayment {Money("6.02")}|{Money("13.98")} [en]", vm.AllocationSummary);
    }

    [Fact]
    public async Task Save_EditingAnExistingPayment_MayKeepItsOwnAllocationOfAFullyPaidBill()
    {
        var bill = WaterBill(46.02m, 10);
        var payment = StoredPayment(46.02m, _water.Id, (bill, 46.02m));
        var vm = await OpenAsync(payment.Id);
        vm.Notes = "checked";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(vm.Errors);
        Assert.Equal("checked", _kit.Payments.Stored.Single().Notes);
    }

    [Fact]
    public async Task Save_InvalidAmounts_ShowAMessageOnTheField()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);

        foreach (var text in new[] { string.Empty, "abc", "0", "1e400", "99999999999999999999", "12,3,4" })
        {
            vm.AmountText = text;
            await vm.SaveCommand.ExecuteAsync(null);

            Assert.NotEmpty(vm.AmountError);
        }

        Assert.Empty(_kit.Payments.Saved);
    }

    [Fact]
    public async Task Save_AcceptsCommaAndDotAsDecimalSeparator()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);

        vm.AmountText = "58,32";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(58.32m, _kit.Payments.Stored.Single().Amount);
    }

    [Fact]
    public async Task Save_TextsAreLimited()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "10";
        vm.Reference = new string('x', InputLimits.Notes + 1);
        vm.Notes = new string('y', InputLimits.Notes + 1);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ReferenceError);
        Assert.NotEmpty(vm.NotesError);
        Assert.Empty(_kit.Payments.Saved);
    }

    [Fact]
    public async Task Save_WithoutContractInSeveralProperties_NeedsAProperty()
    {
        _kit.Kit.Properties.Stored.Add(new Property { Name = "Cottage", Kind = PropertyKind.House });
        var vm = await NewEditorAsync();
        vm.AmountText = "10";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.PropertyError);
        Assert.Empty(_kit.Payments.Saved);
    }

    // ---- Refund ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refund_IsANegativePaymentAllocatedToANegativeBill()
    {
        var correction = WaterBill(-12m, 10);
        correction.Kind = BillKind.Correction;
        var vm = await NewEditorAsync(billId: correction.Id);

        Assert.Equal("-12.00", vm.AmountText);
        Assert.Equal($"PaymentSummaryFull {Money("-12.00")} [en]", vm.AllocationSummary);

        await vm.SaveCommand.ExecuteAsync(null);

        var stored = Assert.Single(_kit.Payments.Stored);
        Assert.Equal(-12m, stored.Amount);
        Assert.Equal(-12m, Assert.Single(stored.Allocations).Amount);
        var state = await StateOf(correction);
        Assert.True(state.IsPaid);
        Assert.Equal(0m, state.Open);
    }

    [Fact]
    public async Task Refund_RefusesAPositiveAllocationOnANegativeBill()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        vm.AmountText = "-46.02";

        await vm.SaveCommand.ExecuteAsync(null);

        // the allocation (+46.02) has the other sign than the payment
        Assert.True(vm.HasAllocationsError);
        Assert.Empty(_kit.Payments.Saved);
    }

    // ---- Fill from contract ---------------------------------------------------------------------------------------

    [Fact]
    public async Task FillReference_WithExactlyOneBill_UsesItsPeriodAndNumber()
    {
        var bill = WaterBill(46.02m, 9, "W-77");
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "46.02";
        await PickAsync(vm, "PaymentBillOption Water 09.2026");
        vm.Reference = "old";

        await vm.FillReferenceCommand.ExecuteAsync(null);

        Assert.Equal("Abon. Nr. C-100, 09.2026", vm.Reference);
        Assert.Equal(bill.Id, vm.AllocationRows.Single().BillId);
    }

    [Fact]
    public async Task FillReference_WithoutABill_LeavesThePeriodEmpty()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);

        await vm.FillReferenceCommand.ExecuteAsync(null);

        Assert.Equal("Abon. Nr. C-100, ", vm.Reference);
    }

    [Fact]
    public async Task FillReference_WithSeveralBills_DoesNotPickOne()
    {
        WaterBill(30m, 9);
        WaterBill(28.32m, 10);
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "58.32";
        await vm.AutoAllocateCommand.ExecuteAsync(null);

        await vm.FillReferenceCommand.ExecuteAsync(null);

        Assert.Equal("Abon. Nr. C-100, ", vm.Reference);
    }

    [Fact]
    public async Task FillReference_WithoutATemplate_TellsTheUser()
    {
        _kit.Supplier.PaymentDetails = null;
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.Reference = "keep";

        await vm.FillReferenceCommand.ExecuteAsync(null);

        Assert.Equal("keep", vm.Reference);
        Assert.Contains(_kit.Kit.Dialogs.Alerts, a => a.Message == "PaymentNoPurposeTemplate [en]");
    }

    // ---- Quick-action flag ----------------------------------------------------------------------------------------

    [Fact]
    public async Task QuickPayment_EditedDate_KeepsTheFlag()
    {
        var bill = WaterBill(46.02m, 10);
        var quick = await _kit.QuickPay.MarkPaidAsync(bill.Id);
        var vm = await OpenAsync(quick!.Id);
        Assert.True(vm.ShowQuickNote);

        vm.PaidOn = Today.AddDays(-4);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(_kit.Payments.Stored.Single().CreatedByQuickAction);
        Assert.Equal(Today.AddDays(-4), _kit.Payments.Stored.Single().PaidOn);
    }

    [Fact]
    public async Task QuickPayment_AmountDifferentFromItsAllocation_ClearsTheFlag()
    {
        var bill = WaterBill(46.02m, 10);
        var quick = await _kit.QuickPay.MarkPaidAsync(bill.Id);
        var vm = await OpenAsync(quick!.Id);

        vm.AmountText = "50.00";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(_kit.Payments.Stored.Single().CreatedByQuickAction);
        Assert.False(vm.ShowQuickNote);
    }

    [Fact]
    public async Task QuickPayment_AllocatedToAnotherBillAsWell_ClearsTheFlag()
    {
        var october = WaterBill(46.02m, 10);
        WaterBill(20m, 11);
        var quick = await _kit.QuickPay.MarkPaidAsync(october.Id);
        var vm = await OpenAsync(quick!.Id);
        vm.AmountText = "66.02";
        await PickAsync(vm, "PaymentBillOption Water 11.2026");

        await vm.SaveCommand.ExecuteAsync(null);

        var stored = _kit.Payments.Stored.Single();
        Assert.Equal(2, stored.Allocations.Count(a => !a.IsDeleted));
        Assert.False(stored.CreatedByQuickAction);
    }

    // ---- Leaving --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CanLeave_WithoutChanges_IsTrueWithoutAsking()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);

        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _kit.Kit.Dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task CanLeave_WithChanges_AsksAndHonoursCancel()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.AmountText = "10";
        _kit.Kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        Assert.False(await vm.CanLeaveAsync());
        Assert.Equal(1, _kit.Kit.Dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task CanLeave_Save_StoresThePaymentFirst()
    {
        var bill = WaterBill(46.02m, 10);
        var vm = await NewEditorAsync(billId: bill.Id);
        vm.Notes = "x";
        _kit.Kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.True(await vm.CanLeaveAsync());
        Assert.Single(_kit.Payments.Stored);
    }

    [Fact]
    public async Task ChangingTheLanguage_DoesNotMakeThePaymentDirty_AndKeepsTheMethod()
    {
        var vm = await NewEditorAsync(contractId: _water.Id);
        vm.SelectedMethod = vm.Methods.First(m => m.Value.Equals(PaymentMethod.Cash));
        vm.IsDirty = false;

        _kit.Kit.Language.SetLanguage("ru");

        Assert.False(vm.IsDirty);
        Assert.Equal(PaymentMethod.Cash, vm.SelectedMethod!.As<PaymentMethod>());
        Assert.Contains("[ru]", vm.SelectedMethod.DisplayName, StringComparison.Ordinal);
    }

    // ---- Opening and deleting -------------------------------------------------------------------------------------

    [Fact]
    public async Task Open_ShowsTheStoredPayment()
    {
        var bill = WaterBill(46.02m, 10);
        var payment = StoredPayment(70m, _water.Id, (bill, 46.02m));
        payment.Method = PaymentMethod.Cash;
        payment.Reference = "ref";

        var vm = await OpenAsync(payment.Id);

        Assert.False(vm.IsNew);
        Assert.Equal("70.00", vm.AmountText);
        Assert.Equal(PaymentMethod.Cash, vm.SelectedMethod!.As<PaymentMethod>());
        Assert.Equal("ref", vm.Reference);
        Assert.Equal("Water", vm.SelectedContractName);
        Assert.Equal($"PaymentSummaryPrepayment {Money("46.02")}|{Money("23.98")} [en]", vm.AllocationSummary);
        Assert.False(vm.IsDirty);
        Assert.True(vm.IsContractReadOnly);
    }

    [Fact]
    public async Task Delete_WithoutAllocations_UsesTheStandardMessage()
    {
        var payment = StoredPayment(20m, _water.Id);
        var vm = await OpenAsync(payment.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Contains(_kit.Kit.Dialogs.Confirmations, c => c.Message == "Dialog_Delete_Message [en]");
        Assert.Contains(payment.Id, _kit.Payments.Deleted);
    }

    [Fact]
    public async Task Delete_CanBeCancelled()
    {
        var payment = StoredPayment(20m, _water.Id);
        var vm = await OpenAsync(payment.Id);
        _kit.Kit.Dialogs.ConfirmResult = false;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Payments.Deleted);
    }

    private async Task PickAsync(PaymentEditViewModel vm, string labelPrefix)
    {
        _kit.Kit.Dialogs.ChooseScript = options => options.FirstOrDefault(o => o.StartsWith(labelPrefix, StringComparison.Ordinal));
        await vm.AddAllocationCommand.ExecuteAsync(null);
    }
}
