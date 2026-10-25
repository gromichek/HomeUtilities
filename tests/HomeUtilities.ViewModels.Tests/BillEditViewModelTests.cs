using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// The bill editor over the real <c>ChargeCalculator</c>. The water contract of <see cref="BillTestKit"/> is calculated as
/// 12.150 m³ × 3.0675 = 37.27 net; VAT 21 % = 7.83; total 45.10. FakeLocalizer renders texts as "key [en]".
/// </summary>
public class BillEditViewModelTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public BillEditViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private async Task<BillEditViewModel> NewEditorAsync(Contract? contract = null)
    {
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);
        if (contract is not null)
        {
            _kit.Kit.Dialogs.ChooseScript = options => options.Contains(contract.Title) ? contract.Title : null;
            await vm.ChooseContractCommand.ExecuteAsync(null);
        }

        return vm;
    }

    private async Task<BillEditViewModel> OpenAsync(Guid billId)
    {
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(billId);
        return vm;
    }

    // ---- Defaults -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task NewBill_ForAContract_TakesTheDefaultsOfTheContract()
    {
        var contract = _kit.AddWaterContract();

        var vm = await NewEditorAsync(contract);

        Assert.True(vm.IsNew);
        Assert.False(vm.IsOneOff);
        Assert.Equal("Sample flat", vm.SelectedPropertyName);
        Assert.Equal("Water", vm.SelectedContractName);
        // today (27 Oct) is inside the reading window 25-28, so the month is October; due day 20 of the month after
        Assert.Equal(new YearMonth(2026, 10), vm.Period);
        Assert.True(vm.HasDueOn);
        Assert.Equal(new DateOnly(2026, 11, 20), vm.DueOn);
        Assert.True(vm.HasIssuedOn);
        Assert.Equal(Today, vm.IssuedOn);
        Assert.Equal("Enum_ServiceKind_ColdWater [en]", vm.SelectedCategoryName);
        Assert.Equal("Water utility Ltd", vm.SelectedSupplierName);
        Assert.Equal(BillKind.Regular, vm.SelectedKind!.As<BillKind>());
        Assert.Equal(Eur, vm.CurrencySymbol);
        Assert.Equal("21", vm.Amounts.VatPercentText);
    }

    [Fact]
    public async Task NewBill_BeforeTheReadingWindow_BelongsToThePreviousMonth()
    {
        var kit = new BillTestKit(new DateOnly(2026, 10, 10));
        var contract = kit.AddWaterContract(tariff: false);
        var vm = kit.CreateEditor();
        await vm.LoadAsync(null);
        kit.Kit.Dialogs.ChooseScript = options => options.Contains("Water") ? "Water" : null;

        await vm.ChooseContractCommand.ExecuteAsync(null);

        // readings of September were submitted in September; the bill for them arrives in October
        Assert.Equal(new YearMonth(2026, 9), vm.Period);
        Assert.Equal(new DateOnly(2026, 10, 20), vm.DueOn);
    }

    [Fact]
    public async Task NewBill_ForAnInAdvanceContract_BelongsToTheNextMonth_AndIsDueBeforeIt()
    {
        var contract = _kit.AddWaterContract("Internet", tariff: false, timing: BillingTiming.InAdvance, dueDay: 15);

        var vm = await NewEditorAsync(contract);

        // paid in October for November
        Assert.Equal(new YearMonth(2026, 11), vm.Period);
        Assert.Equal(new DateOnly(2026, 10, 15), vm.DueOn);
    }

    [Fact]
    public async Task ChangingThePeriod_MovesTheDueDate_UntilTheUserSetsIt()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);

        vm.PreviousPeriodCommand.Execute(null);
        Assert.Equal(new YearMonth(2026, 9), vm.Period);
        Assert.Equal(new DateOnly(2026, 10, 20), vm.DueOn);

        vm.DueOn = new DateOnly(2026, 10, 31);
        vm.NextPeriodCommand.Execute(null);

        Assert.Equal(new DateOnly(2026, 10, 31), vm.DueOn);
    }

    [Fact]
    public async Task ContractWithoutDueDay_LeavesTheDueDateOff()
    {
        var contract = _kit.AddWaterContract(tariff: false, dueDay: null);

        var vm = await NewEditorAsync(contract);

        Assert.False(vm.HasDueOn);
    }

    [Fact]
    public async Task OneOffExpense_StartsInTheCurrentMonthWithoutContractDefaults()
    {
        var vm = await NewEditorAsync();

        vm.SelectedTypeIndex = 1;

        Assert.True(vm.IsOneOff);
        Assert.Equal(new YearMonth(2026, 10), vm.Period);
        Assert.Null(vm.SelectedCategory);
        Assert.Equal(BillKind.OneOff, vm.SelectedKind!.As<BillKind>());
        Assert.False(vm.IsKindEditable);
        Assert.False(vm.ShowComparison);
    }

    [Fact]
    public async Task ServicePeriod_StartsWithTheMonthOfThePeriod()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);

        vm.HasServicePeriod = true;

        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (vm.ServiceFrom, vm.ServiceTo));
    }

    [Fact]
    public async Task NewBill_FromThePresets_OfTheChargesScreen()
    {
        var contract = _kit.AddWaterContract();
        var vm = _kit.CreateEditor();
        vm.PresetContractId = contract.Id;
        vm.PresetPeriod = new YearMonth(2026, 9);
        vm.PresetGross = 45.10m;

        await vm.LoadAsync(null);

        Assert.Equal("Water", vm.SelectedContractName);
        Assert.Equal(new YearMonth(2026, 9), vm.Period);
        Assert.Equal(new DateOnly(2026, 10, 20), vm.DueOn);
        // the suggestion is a hint only, never a value
        Assert.Equal("45.10", vm.Amounts.GrossPlaceholder);
        Assert.Equal(string.Empty, vm.Amounts.GrossText);
        Assert.False(vm.IsDirty);
    }

    // ---- Amounts --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Amounts_NetAndVatPercent_DeriveVatAndTotal_AndAreSaved()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);

        vm.Amounts.VatPercentText = "21";
        vm.Amounts.NetText = "10,00";
        await vm.SaveCommand.ExecuteAsync(null);

        // 10.00 + 21 % = 2.10 VAT; total 12.10
        var saved = Assert.Single(_kit.Bills.Saved);
        Assert.Equal((10.00m, 21m, 2.10m, 12.10m), (saved.Net, saved.VatPercent, saved.Vat, saved.Gross));
    }

    [Fact]
    public async Task Amounts_TotalAlone_IsEnough()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.VatPercentText = string.Empty;

        vm.Amounts.GrossText = "46,02";
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.Saved);
        Assert.Equal((null, null, null, 46.02m), (saved.Net, saved.VatPercent, saved.Vat, saved.Gross));
    }

    [Fact]
    public async Task Amounts_WithoutAnyAmount_ShowsAnErrorUnderTheTotal_AndSavesNothing()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.Equal("Error_amount_gross_required [en]", vm.GrossError);
    }

    [Fact]
    public async Task Amounts_Nonsense_ShowsAMessage_AndDoesNotThrow()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);

        vm.Amounts.GrossText = "12abc";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.Equal("Error_common_number_invalid [en]", vm.GrossError);
    }

    [Fact]
    public async Task NegativeTotal_IsRefusedForARegularBill()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);

        vm.Amounts.GrossText = "-5";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.Equal("Error_bill_negative_not_allowed [en]", vm.GrossError);
    }

    // ---- One-off --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task OneOff_RequiresATitleAndACategory()
    {
        var vm = await NewEditorAsync();
        vm.SelectedTypeIndex = 1;
        vm.Amounts.GrossText = "120";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.Equal("Error_bill_title_required [en]", vm.TitleError);
        Assert.Equal("Error_bill_category_required [en]", vm.CategoryError);
    }

    [Fact]
    public async Task OneOff_WithTitleAndCategory_IsSavedWithoutAContract()
    {
        var vm = await NewEditorAsync();
        vm.SelectedTypeIndex = 1;
        _kit.Kit.Dialogs.ChooseScript = options => options.FirstOrDefault(o => o.Contains("ColdWater", StringComparison.Ordinal));
        await vm.ChooseCategoryCommand.ExecuteAsync(null);
        vm.BillTitle = "Boiler repair";
        vm.Amounts.GrossText = "120";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.Saved);
        Assert.Equal((BillKind.OneOff, null, "Boiler repair", 120m), (saved.Kind, saved.ContractId, saved.Title, saved.Gross));
        Assert.Equal(_kit.Kit.Property.Id, saved.PropertyId);
        Assert.Equal("BillOneOffTitle Boiler repair [en]", vm.Title);
        Assert.False(vm.IsNew);
    }

    [Fact]
    public async Task ContractBill_WithoutAContract_AsksForOne()
    {
        var vm = await NewEditorAsync();
        vm.Amounts.GrossText = "10";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.Equal("Error_bill_contract_kind_mismatch [en]", vm.ContractError);
    }

    // ---- Comparison -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Comparison_ShowsTheDifferenceToTheCalculation()
    {
        var contract = _kit.AddWaterContract();
        var vm = await NewEditorAsync(contract);
        Assert.True(vm.ShowComparison);

        vm.Amounts.GrossText = "46,02";

        // calculated 45.10; bill 46.02 -> +0.92 (+2.0 %)
        Assert.Equal($"45.10 {Eur}", vm.CalculatedText);
        Assert.Equal($"+0.92 {Eur} (+2.0 %)", vm.DifferenceText);
        Assert.False(vm.DifferenceHigher);
        Assert.False(vm.DifferenceLower);
    }

    [Theory]
    [InlineData("50,00", true, false)] // +4.90 = +10.9 %
    [InlineData("47,00", false, false)] // +1.90 = +4.2 %
    [InlineData("40,00", false, true)] // -5.10 = -11.3 %
    public async Task Comparison_ColoursAboveFivePercentRed_AndBelowMinusFiveGreen(string gross, bool higher, bool lower)
    {
        var contract = _kit.AddWaterContract();
        var vm = await NewEditorAsync(contract);

        vm.Amounts.GrossText = gross;

        Assert.Equal((higher, lower), (vm.DifferenceHigher, vm.DifferenceLower));
    }

    [Fact]
    public async Task Comparison_QuantityOnTheBill_ShowsTheWaterLosses()
    {
        var contract = _kit.AddWaterContract();
        var vm = await NewEditorAsync(contract);

        vm.ConsumptionText = "12,500";

        // bill 12.500 m3 against metered 12.150 m3 -> 0.350 m3
        Assert.Equal("12.150 Enum_MeterUnit_M3 [en]", vm.MeteredText);
        Assert.Equal("0.350 Enum_MeterUnit_M3 [en]", vm.QuantityDifferenceText);
    }

    [Fact]
    public async Task Comparison_ContractWithoutTariff_IsHidden()
    {
        var contract = _kit.AddWaterContract(tariff: false);

        var vm = await NewEditorAsync(contract);

        Assert.False(vm.ShowComparison);
    }

    [Fact]
    public async Task Comparison_ShowCharges_OpensTheChargesOfThePropertyAndMonth()
    {
        var contract = _kit.AddWaterContract();
        var vm = await NewEditorAsync(contract);

        await vm.ShowChargesCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.ChargesFor, route);
        Assert.Equal(contract.Id, parameters![EditorNavigation.ContractIdParameter]);
        Assert.Equal(_kit.Kit.Property.Id, parameters[EditorNavigation.PropertyIdParameter]);
        Assert.Equal("2026-10", parameters[ChargesViewModel.MonthParameter]);
    }

    // ---- Snapshot -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Save_StoresTheCurrentCalculationAsTheSnapshot()
    {
        var contract = _kit.AddWaterContract();
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "46,02";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.Saved);
        Assert.Equal(45.10m, saved.CalculatedSnapshot!.TotalGross);
        Assert.Equal(new YearMonth(2026, 10), saved.CalculatedSnapshot.Period);
        Assert.False(vm.ShowSnapshotChanged);
    }

    [Fact]
    public async Task ChangedCalculation_IsReported_ButTheComparisonKeepsUsingTheSnapshot_UntilItIsUpdated()
    {
        var contract = _kit.AddWaterContract();
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m, snapshot: _kit.Calculation(contract, new YearMonth(2026, 10), 45.10m));
        // the tariff price rises: 12.150 m3 x 3.1 = 37.665 -> 37.67 net; VAT 7.91; total 45.58
        _kit.Kit.Tariffs.Stored[0].Rates[0].Price = 3.1m;

        var vm = await OpenAsync(bill.Id);

        Assert.Equal($"BillSnapshotChanged 45.10 {Eur}|45.58 {Eur} [en]", vm.SnapshotChangedText);
        Assert.Equal($"45.10 {Eur}", vm.CalculatedText);
        Assert.Equal($"+0.92 {Eur} (+2.0 %)", vm.DifferenceText);

        vm.UpdateSnapshotCommand.Execute(null);

        Assert.True(vm.IsDirty);
        Assert.False(vm.ShowSnapshotChanged);
        Assert.Equal($"45.58 {Eur}", vm.CalculatedText);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(45.58m, _kit.Bills.Saved.Last().CalculatedSnapshot!.TotalGross);
    }

    [Fact]
    public async Task SavingAnOldBill_KeepsItsSnapshot()
    {
        var contract = _kit.AddWaterContract();
        var snapshot = _kit.Calculation(contract, new YearMonth(2026, 10), 45.10m);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m, snapshot: snapshot);
        _kit.Kit.Tariffs.Stored[0].Rates[0].Price = 3.1m;
        var vm = await OpenAsync(bill.Id);

        vm.Notes = "checked";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(45.10m, _kit.Bills.Saved.Single().CalculatedSnapshot!.TotalGross);
    }

    [Fact]
    public async Task ChangingThePeriodOfASavedBill_RecalculatesTheSnapshotForTheNewMonth()
    {
        var contract = _kit.AddWaterContract();
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m, snapshot: _kit.Calculation(contract, new YearMonth(2026, 10), 45.10m));
        var vm = await OpenAsync(bill.Id);

        vm.PreviousPeriodCommand.Execute(null);
        await vm.PendingRefresh;
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = _kit.Bills.Saved.Single();
        Assert.Equal(new YearMonth(2026, 9), saved.Period);
        Assert.Equal(new YearMonth(2026, 9), saved.CalculatedSnapshot!.Period);
    }

    // ---- Lines ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Lines_AddedInTheDialog_AreSavedWithTheBill_AndWarnWhenTheyDoNotAddUp()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "46,02";
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var line = (BillLineEditorViewModel)editor;
            line.Description = "Water 12 m3";
            line.Amounts.GrossText = "30";
            line.SaveCommand.Execute(null);
        };

        await vm.AddLineCommand.ExecuteAsync(null);

        Assert.Single(vm.LineRows);
        Assert.True(vm.LinesExpanded);
        Assert.Equal($"BillLinesMismatch 30.00 {Eur}|46.02 {Eur} [en]", vm.LinesWarning);
        Assert.True(vm.IsDirty);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.Saved);
        var line = Assert.Single(saved.Lines);
        Assert.Equal(("Water 12 m3", 30m, saved.Id), (line.Description, line.Gross, line.BillId));
    }

    [Fact]
    public async Task Lines_AddingUpToTheTotal_RemovesTheWarning()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "30";
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var line = (BillLineEditorViewModel)editor;
            line.Description = "Water";
            line.Amounts.GrossText = "30";
            line.SaveCommand.Execute(null);
        };

        await vm.AddLineCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.LinesWarning);
    }

    [Fact]
    public async Task Lines_Delete_AsksFirst_AndKeepsTheLineWhenCancelled()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "30";
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var line = (BillLineEditorViewModel)editor;
            line.Description = "Water";
            line.Amounts.GrossText = "30";
            line.SaveCommand.Execute(null);
        };
        await vm.AddLineCommand.ExecuteAsync(null);

        _kit.Kit.Dialogs.ConfirmResult = false;
        vm.LineRows[0].RemoveCommand.Execute(null);
        Assert.Single(vm.LineRows);

        _kit.Kit.Dialogs.ConfirmResult = true;
        vm.LineRows[0].RemoveCommand.Execute(null);
        Assert.Empty(vm.LineRows);
    }

    // ---- Custom fields --------------------------------------------------------------------------------------------

    [Fact]
    public async Task CustomFields_OfTheContract_AreFilledInAndSavedByKind()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var losses = new CustomFieldDefinition { OwnerId = contract.Id, Name = "Losses m3", Kind = CustomFieldKind.Number, Target = CustomFieldTarget.Bill, SortOrder = 0 };
        var reference = new CustomFieldDefinition { OwnerId = contract.Id, Name = "Reference", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.Bill, SortOrder = 1 };
        var read = new CustomFieldDefinition { OwnerId = contract.Id, Name = "Read on", Kind = CustomFieldKind.Date, Target = CustomFieldTarget.Bill, SortOrder = 2 };
        var lineOnly = new CustomFieldDefinition { OwnerId = contract.Id, Name = "Per line", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.BillLine, SortOrder = 3 };
        contract.CustomFields.AddRange([losses, reference, read, lineOnly]);
        var vm = await NewEditorAsync(contract);

        Assert.Equal(["Losses m3", "Reference", "Read on"], vm.FieldRows.Select(r => r.Name));
        vm.Amounts.GrossText = "10";
        vm.FieldRows[0].Text = "0,350";
        vm.FieldRows[1].Text = "  R-77 ";
        vm.FieldRows[2].HasDate = true;
        vm.FieldRows[2].Date = new DateOnly(2026, 10, 26);
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.Saved);
        Assert.Equal(
            [("Losses m3", "0.350", losses.Id), ("Reference", "R-77", reference.Id), ("Read on", "2026-10-26", read.Id)],
            saved.CustomFields.Select(f => (f.Name, f.Value, f.DefinitionId)).ToList());
    }

    [Fact]
    public async Task CustomFields_ANumberThatIsNotOne_ShowsAnErrorOnItsRow()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        contract.CustomFields.Add(new CustomFieldDefinition { OwnerId = contract.Id, Name = "Losses m3", Kind = CustomFieldKind.Number, Target = CustomFieldTarget.Bill });
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "10";

        vm.FieldRows[0].Text = "lots";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        Assert.Equal("Error_common_number_invalid [en]", vm.FieldRows[0].ErrorText);
    }

    [Fact]
    public async Task CustomFields_ValuesOfARemovedDefinition_AreKept()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 10m);
        bill.CustomFields.Add(new CustomFieldValue(Guid.NewGuid(), "Old field", CustomFieldKind.Text, "kept"));

        var vm = await OpenAsync(bill.Id);

        Assert.Equal("Old field", Assert.Single(vm.FieldRows).Name);
        vm.Notes = "x";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("kept", Assert.Single(_kit.Bills.Saved.Single().CustomFields).Value);
    }

    // ---- Saving, leaving, deleting --------------------------------------------------------------------------------

    [Fact]
    public async Task Save_KeepsThePageOpen_AndShowsTheSavedMessage()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.IsHostedInPage = true;
        vm.Amounts.GrossText = "46,02";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Equal(0, _kit.Kit.Navigation.BackCount);
        Assert.Contains("Saved [en]", _kit.Kit.Toast.Messages);
        Assert.True(vm.ShowPaymentCard);
    }

    [Fact]
    public async Task CanLeaveAsync_WithUnsavedChanges_AsksAndSavesOnRequest()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "46,02";
        _kit.Kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        var canLeave = await vm.CanLeaveAsync();

        Assert.True(canLeave);
        Assert.Equal(1, _kit.Kit.Dialogs.UnsavedAsked);
        Assert.Single(_kit.Bills.Saved);
    }

    [Fact]
    public async Task CanLeaveAsync_WithoutChanges_LeavesAtOnce()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 10m);
        var vm = await OpenAsync(bill.Id);

        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _kit.Kit.Dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task CanLeaveAsync_CancelStays()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "46,02";
        _kit.Kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        Assert.False(await vm.CanLeaveAsync());
        Assert.Empty(_kit.Bills.Saved);
    }

    [Fact]
    public async Task Delete_WithoutPayments_UsesTheStandardMessage()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 10m);
        var vm = await OpenAsync(bill.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("Dialog_Delete_Message [en]", Assert.Single(_kit.Kit.Dialogs.Confirmations).Message);
        Assert.Equal([bill.Id], _kit.Bills.Deleted);
    }

    [Fact]
    public async Task Delete_WithPayments_SaysSo_RemovesQuickPaymentsAndKeepsOtherMoneyAsPrepayment()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 50m);
        var manual = _kit.AddPayment(20m, (bill, 20m));
        var quick = (await _kit.QuickPay.MarkPaidAsync(bill.Id))!;
        var vm = await OpenAsync(bill.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("BillDeleteWithPayments [en]", Assert.Single(_kit.Kit.Dialogs.Confirmations).Message);
        Assert.Equal([bill.Id], _kit.Bills.Deleted);
        Assert.DoesNotContain(_kit.Payments.Stored, p => p.Id == quick.Id);
        var kept = Assert.Single(_kit.Payments.Stored);
        Assert.Equal((manual.Id, 20m), (kept.Id, kept.Amount));
        Assert.DoesNotContain(kept.Allocations, a => !a.IsDeleted);
    }

    [Fact]
    public async Task Delete_Cancelled_ChangesNothing()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 10m);
        var vm = await OpenAsync(bill.Id);
        _kit.Kit.Dialogs.ConfirmResult = false;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Deleted);
    }

    // ---- Payment card ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task PaymentCard_ShowsTheStatusAndTheOpenAmount()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m, due: new DateOnly(2026, 11, 20));
        _kit.AddPayment(20m, (bill, 20m));

        var vm = await OpenAsync(bill.Id);

        Assert.Equal($"BillBadgePartial 26.02 {Eur} [en]", vm.StatusText);
        Assert.Equal(BillBadgeKind.PartiallyPaid, vm.StatusKind);
        Assert.Equal($"20.00 {Eur}", vm.PaidAmountText);
        Assert.Equal($"26.02 {Eur}", vm.OpenAmountText);
        Assert.True(vm.CanMarkPaid);
        Assert.Single(vm.PaymentRows);
    }

    [Fact]
    public async Task Paid_MarksTheBillPaid_ShowsUndo_AndUndoRestoresTheOpenAmount()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m);
        var vm = await OpenAsync(bill.Id);
        _kit.Kit.Toast.ActionResult = (_, _) => false;

        await vm.MarkPaidCommand.ExecuteAsync(null);

        Assert.Equal(BillBadgeKind.Paid, vm.StatusKind);
        Assert.False(vm.CanMarkPaid);
        var call = Assert.Single(_kit.Kit.Toast.Calls);
        Assert.Equal(("BillMarkedPaid [en]", "BillUndo [en]"), (call.Message, call.Action));
        Assert.Single(_kit.Payments.Stored, p => p.CreatedByQuickAction && p.Amount == 46.02m);

        // the same again, but this time the user taps Undo
        var second = _kit.AddBill(contract, new YearMonth(2026, 9), 30m, due: new DateOnly(2026, 11, 20));
        var vm2 = await OpenAsync(second.Id);
        _kit.Kit.Toast.ActionResult = (_, _) => true;

        await vm2.MarkPaidCommand.ExecuteAsync(null);

        Assert.Equal(BillBadgeKind.Unpaid, vm2.StatusKind);
        Assert.True(vm2.CanMarkPaid);
        Assert.DoesNotContain(_kit.Payments.Stored, p => p.Allocations.Any(a => a.BillId == second.Id));
    }

    [Fact]
    public async Task Paid_WithUnsavedChanges_AsksToSaveFirst()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m);
        var vm = await OpenAsync(bill.Id);
        vm.Amounts.GrossText = "50";

        await vm.MarkPaidCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Payments.All);
        Assert.Equal("BillSaveFirst [en]", Assert.Single(_kit.Kit.Dialogs.Alerts).Message);
    }

    [Fact]
    public async Task RemovePayment_AsksWithAmountAndDate_ThenTheBillIsUnpaid()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m);
        await _kit.QuickPay.MarkPaidAsync(bill.Id);
        var vm = await OpenAsync(bill.Id);

        vm.PaymentRows[0].RemoveCommand.Execute(null);
        await Task.Delay(50);

        var (title, message) = Assert.Single(_kit.Kit.Dialogs.Confirmations);
        Assert.Equal($"BillRemovePaymentTitle 46.02 {Eur}|27.10.2026 [en]", title);
        Assert.Equal("BillRemovePaymentHidden [en]", message);
        Assert.Empty(_kit.Payments.Stored);
        Assert.Equal(BillBadgeKind.Unpaid, vm.StatusKind);
    }

    [Fact]
    public async Task RemovePayment_OfASharedPayment_SaysTheRestStaysAsPrepayment()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m);
        var other = _kit.AddBill(contract, new YearMonth(2026, 9), 10m);
        _kit.AddPayment(56.02m, (bill, 46.02m), (other, 10m));
        var vm = await OpenAsync(bill.Id);

        vm.PaymentRows[0].RemoveCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("BillRemovePaymentRest [en]", Assert.Single(_kit.Kit.Dialogs.Confirmations).Message);
        Assert.Single(_kit.Payments.Stored);
        Assert.Equal(BillBadgeKind.Unpaid, vm.StatusKind);
    }

    // ---- Documents ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Document_Chosen_IsStoredWhenTheBillIsSaved_AndOpensWithTheViewer()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.Amounts.GrossText = "10";
        _kit.Kit.Dialogs.ChooseScript = options => options.First(o => o.Contains("BillAttachmentPdf", StringComparison.Ordinal));
        _kit.Kit.Media.Next = FakeMediaService.Photo("bill.pdf", "application/pdf", 9, 9);

        await vm.AddAttachmentCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Kit.Attachments.Added);
        Assert.Single(vm.AttachmentRows);
        Assert.True(vm.AttachmentRows[0].IsPending);

        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(_kit.Kit.Attachments.Added);
        Assert.Equal(("bill.pdf", AttachmentOwnerKindBill), (added.Meta.FileName, added.Meta.OwnerKind));
        Assert.Equal(added.Meta.Id, Assert.Single(_kit.Bills.Saved.Single().AttachmentIds));
        Assert.False(vm.AttachmentRows[0].IsPending);
    }

    private static readonly HomeUtilities.Core.Attachments.AttachmentOwnerKind AttachmentOwnerKindBill = HomeUtilities.Core.Attachments.AttachmentOwnerKind.Bill;

    [Fact]
    public async Task Document_Cancelled_AddsNothing()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        vm.IsDirty = false;
        _kit.Kit.Dialogs.ChooseScript = _ => null;

        await vm.AddAttachmentCommand.ExecuteAsync(null);

        Assert.Empty(vm.AttachmentRows);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Document_PermissionDenied_ShowsAMessage()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await NewEditorAsync(contract);
        _kit.Kit.Dialogs.ChooseScript = options => options.First(o => o.Contains("PhotoTake", StringComparison.Ordinal));
        _kit.Kit.Media.Failure = new MediaPermissionDeniedException();

        await vm.AddAttachmentCommand.ExecuteAsync(null);

        Assert.Equal("PhotoPermissionDenied [en]", vm.AttachmentError);
        Assert.Empty(vm.AttachmentRows);
    }

    // ---- Loading a saved bill -------------------------------------------------------------------------------------

    [Fact]
    public async Task SavedBill_ShowsItsValues_AndHasNoContractPicker()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var bill = _kit.AddBill(contract, new YearMonth(2026, 10), 46.02m);
        bill.Number = "W-77";
        bill.Notes = "paid by phone";
        bill.ServiceFrom = new DateOnly(2026, 9, 1);
        bill.ServiceTo = new DateOnly(2026, 10, 31);

        var vm = await OpenAsync(bill.Id);

        Assert.False(vm.IsNew);
        Assert.False(vm.ShowTypePicker);
        Assert.True(vm.IsContractReadOnly);
        Assert.Equal("Water", vm.SelectedContractName);
        Assert.Equal(("W-77", "paid by phone", true), (vm.Number, vm.Notes, vm.HasServicePeriod));
        Assert.Equal("46.02", vm.Amounts.GrossText);
        Assert.False(vm.IsDirty);
        Assert.Equal("BillEditTitle Water 10.2026 [en]", vm.Title);
    }
}
