using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// Equalized plans over the real <c>ChargeCalculator</c>. The water contract of <see cref="BillTestKit"/> is used as the
/// "electricity" contract: October 2026 costs 12.150 m³ × 3.0675 = 37.27 net; VAT 21 % = 7.83; total 45.10. Today is 27 Oct
/// 2026, so the current month is October. FakeLocalizer renders texts as "key [en]".
/// </summary>
public class EqualizedViewModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public EqualizedViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private Contract AddEqualizedContract()
    {
        var contract = _kit.AddWaterContract("Electricity");
        contract.BillingMode = BillingMode.Equalized;
        return contract;
    }

    private EqualizedPlan AddPlan(Contract contract, string from, decimal amount, string? to = null)
    {
        var plan = new EqualizedPlan
        {
            ContractId = contract.Id,
            ValidFrom = DateOnly.Parse(from, CultureInfo.InvariantCulture),
            ValidTo = to is null ? null : DateOnly.Parse(to, CultureInfo.InvariantCulture),
            MonthlyAmount = amount,
        };
        _kit.Kit.Plans.All.Add(plan);
        return plan;
    }

    private Bill AddEqualizedBill(Contract contract, YearMonth period, decimal gross, BillKind kind = BillKind.Equalized)
    {
        var bill = _kit.AddBill(contract, period, gross);
        bill.Kind = kind;
        return bill;
    }

    private async Task<ContractEditViewModel> OpenContractAsync(Contract contract)
    {
        var vm = _kit.Kit.CreateContractEditor();
        await vm.LoadAsync(contract.PropertyId, contract.Id);
        return vm;
    }

    // ---- Settlement service -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Service_AddsTheDifferencesOfTheCompleteMonthsAndCountsTheOthers()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-09-01", 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 9), 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 40m);

        var report = await _kit.Kit.Settlement.LoadAsync(contract.Id);

        // September has no earlier reading (incomplete, left out); October: 45.10 actual - 40.00 billed = +5.10
        var settlement = report.Settlement;
        Assert.Equal((new YearMonth(2026, 9), new YearMonth(2026, 10)), (settlement.From, settlement.To));
        Assert.Equal(1, settlement.MissingMonths);
        Assert.Equal((40m, 45.10m, 5.10m), (settlement.TotalBilled, settlement.TotalActual, settlement.Expected));
    }

    [Fact]
    public async Task Service_StartsAfterTheLastSettlementBill()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2025-01-01", 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 8), -3m, BillKind.Settlement);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 40m);

        var settlement = (await _kit.Kit.Settlement.LoadAsync(contract.Id)).Settlement;

        Assert.Equal(new YearMonth(2026, 9), settlement.From);
        Assert.Equal(new YearMonth(2026, 10), settlement.To);
    }

    // ---- Contract editor: the Equalized plan card -------------------------------------------------------------------

    [Fact]
    public async Task Card_IsShownForTheEqualizedModeOnly_AndListsThePlansNewestFirst()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-06-01", 40m, "2026-09-30");
        AddPlan(contract, "2026-10-01", 45m);

        var vm = await OpenContractAsync(contract);

        Assert.True(vm.ShowEqualizedCard);
        Assert.False(vm.ShowTaxCard);
        Assert.Equal(
            ["EqPlanRow 01.10.2026|45.00 Currency_Symbol_EUR [en] [en]", "EqPlanRowTo 01.06.2026|30.09.2026|40.00 Currency_Symbol_EUR [en] [en]"],
            vm.PlanRows.Select(r => r.DisplayText));
        Assert.False(vm.HasNoPlans);

        vm.SelectedBillingMode = vm.BillingModes.First(m => m.Value.Equals(BillingMode.Actual));
        Assert.False(vm.ShowEqualizedCard);
    }

    [Fact]
    public async Task Card_SummaryNamesTheExpectedSurchargeAndTheMonthsWithoutReadings()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-09-01", 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 9), 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 40m);

        var vm = await OpenContractAsync(contract);

        Assert.True(vm.ShowSettlementSummary);
        Assert.StartsWith("EqSummarySurcharge 5.10 Currency_Symbol_EUR [en]|09.2026|10.2026 [en]", vm.PlanSummaryText);
        Assert.EndsWith("EqSummaryIncomplete 1 [en]", vm.PlanSummaryText);
        Assert.True(vm.ShowSettlementCommand.CanExecute(null));
    }

    [Fact]
    public async Task Card_SummaryNamesARefund()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 50m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 50m);

        var vm = await OpenContractAsync(contract);

        Assert.StartsWith("EqSummaryRefund 4.90 Currency_Symbol_EUR [en]|10.2026|10.2026 [en]", vm.PlanSummaryText);
    }

    [Fact]
    public async Task Card_ShowSettlementOpensThePageOfTheContract()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);
        var vm = await OpenContractAsync(contract);

        await vm.ShowSettlementCommand.ExecuteAsync(null);

        var visit = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.EqualizedSettlement, visit.Route);
        Assert.Equal(contract.Id, visit.Parameters![EditorNavigation.ContractIdParameter]);
    }

    [Fact]
    public async Task Card_AWrongModeOrANewContractHasNoSettlementSummary()
    {
        var contract = _kit.AddWaterContract();
        var vm = await OpenContractAsync(contract);
        Assert.False(vm.ShowSettlementSummary);

        var fresh = _kit.Kit.CreateContractEditor();
        await fresh.LoadAsync(_kit.Kit.Property.Id, null);
        Assert.True(fresh.ShowPlanSaveFirst);
        Assert.False(fresh.CanEditPlans);
        Assert.False(fresh.AddPlanCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddPlan_SavesItAndEndsTheRunningPlanTheDayBefore()
    {
        var contract = AddEqualizedContract();
        var running = AddPlan(contract, "2026-06-01", 40m);
        var vm = await OpenContractAsync(contract);
        _kit.Kit.Dialogs.EditorScript = e =>
        {
            var dialog = (EqualizedPlanEditorViewModel)e;
            dialog.ValidFrom = new DateOnly(2026, 12, 1);
            dialog.AmountText = "45.50";
            dialog.Note = " new price ";
            dialog.SaveCommand.Execute(null);
        };

        await vm.AddPlanCommand.ExecuteAsync(null);

        var plans = _kit.Kit.Plans.Stored.OrderBy(p => p.ValidFrom).ToList();
        Assert.Equal(2, plans.Count);
        Assert.Equal(new DateOnly(2026, 11, 30), plans[0].ValidTo);
        Assert.Equal(running.Id, plans[0].Id);
        Assert.Equal((contract.Id, new DateOnly(2026, 12, 1), (DateOnly?)null, 45.50m, "new price"), (plans[1].ContractId, plans[1].ValidFrom, plans[1].ValidTo, plans[1].MonthlyAmount, plans[1].Notes));
        Assert.Equal(2, vm.PlanRows.Count);
        Assert.True(EqualizedPlanValidator.ValidateSet(plans).IsValid);
    }

    [Fact]
    public async Task AddPlan_NewPlanDefaultsToTheNextMonth()
    {
        var contract = AddEqualizedContract();
        var vm = await OpenContractAsync(contract);
        EqualizedPlanEditorViewModel? seen = null;
        _kit.Kit.Dialogs.EditorScript = e => seen = (EqualizedPlanEditorViewModel)e;

        await vm.AddPlanCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 11, 1), seen!.ValidFrom);
        Assert.Empty(_kit.Kit.Plans.Stored);
    }

    [Fact]
    public async Task AddPlan_RejectsAnOverlapAndAnInvalidAmount_AndKeepsTheDialogOpen()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-12-01", 45m);
        var vm = await OpenContractAsync(contract);
        EqualizedPlanEditorViewModel? dialogSeen = null;
        _kit.Kit.Dialogs.EditorScript = e =>
        {
            var dialog = dialogSeen = (EqualizedPlanEditorViewModel)e;
            dialog.ValidFrom = new DateOnly(2026, 6, 1);
            dialog.AmountText = "abc";
            dialog.SaveCommand.Execute(null);
            Assert.False(dialog.Saved);
            Assert.NotEmpty(dialog.AmountError);

            dialog.AmountText = "40";
            dialog.SaveCommand.Execute(null);
            Assert.False(dialog.Saved);
            Assert.Equal("Error_equalized_plan_overlap [en]", dialog.ValidFromError);

            dialog.AmountText = "0";
            dialog.ValidFrom = new DateOnly(2026, 1, 1);
            dialog.HasValidTo = true;
            dialog.ValidTo = new DateOnly(2026, 5, 31);
            dialog.SaveCommand.Execute(null);
            Assert.Equal("Error_equalized_plan_amount_not_positive [en]", dialog.AmountError);
        };

        await vm.AddPlanCommand.ExecuteAsync(null);

        Assert.False(dialogSeen!.Saved);
        Assert.Single(_kit.Kit.Plans.Stored);
    }

    [Fact]
    public async Task EditPlan_ChangesTheAmountOfTheSamePlan()
    {
        var contract = AddEqualizedContract();
        var plan = AddPlan(contract, "2026-06-01", 40m);
        var vm = await OpenContractAsync(contract);
        _kit.Kit.Dialogs.EditorScript = e =>
        {
            var dialog = (EqualizedPlanEditorViewModel)e;
            Assert.Equal("40.00", dialog.AmountText);
            dialog.AmountText = "41,5".Replace(',', '.');
            dialog.SaveCommand.Execute(null);
        };

        vm.PlanRows[0].EditCommand.Execute(null);
        await Task.Yield();

        var stored = Assert.Single(_kit.Kit.Plans.Stored);
        Assert.Equal((plan.Id, 41.5m), (stored.Id, stored.MonthlyAmount));
    }

    [Fact]
    public async Task RemovePlan_AsksFirst_ThenSoftDeletesIt()
    {
        var contract = AddEqualizedContract();
        var plan = AddPlan(contract, "2026-06-01", 40m);
        var vm = await OpenContractAsync(contract);
        _kit.Kit.Dialogs.ConfirmResult = false;

        vm.PlanRows[0].RemoveCommand.Execute(null);
        await Task.Yield();
        Assert.Single(_kit.Kit.Plans.Stored);

        _kit.Kit.Dialogs.ConfirmResult = true;
        vm.PlanRows[0].RemoveCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(_kit.Kit.Plans.Stored);
        Assert.True(_kit.Kit.Plans.All.Single(p => p.Id == plan.Id).IsDeleted);
        Assert.True(vm.HasNoPlans);
    }

    // ---- Settlement page --------------------------------------------------------------------------------------------

    private async Task<EqualizedSettlementViewModel> OpenSettlementAsync(Contract contract)
    {
        var vm = new EqualizedSettlementViewModel(
            _kit.Kit.Settlement, _kit.Kit.Contracts, _kit.Kit.Clock, _kit.Kit.Localizer, _kit.Kit.Navigation);
        await vm.LoadAsync(contract.Id);
        return vm;
    }

    [Fact]
    public async Task Page_ShowsTheMonthsTheTotalsAndTheResultInWords()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-09-01", 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 9), 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 40m);

        var vm = await OpenSettlementAsync(contract);

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(["09.2026", "10.2026"], vm.Rows.Select(r => r.PeriodText));
        Assert.False(vm.Rows[0].IsComplete);
        Assert.Equal("EqReadingsMissing [en]", vm.Rows[0].MissingText);
        Assert.Equal("–", vm.Rows[0].DifferenceText);
        Assert.True(vm.Rows[1].IsComplete);
        Assert.Equal("40.00 Currency_Symbol_EUR [en]", vm.Rows[1].BilledText);
        Assert.Equal("45.10 Currency_Symbol_EUR [en]", vm.Rows[1].ActualText);
        Assert.Equal("+5.10 Currency_Symbol_EUR [en]", vm.Rows[1].DifferenceText);
        Assert.True(vm.Rows[1].IsSurcharge);
        Assert.Equal("EqWordsSurcharge 5.10 Currency_Symbol_EUR [en] [en]", vm.ResultText);
        Assert.True(vm.IsSurcharge);
        Assert.False(vm.IsRefund);
        Assert.Equal("EqIncomplete 1 [en]", vm.IncompleteText);
        Assert.True(vm.HasIncompleteText);
        Assert.Equal("EqSettlementPageTitle Electricity [en]", vm.Title);
    }

    [Fact]
    public async Task Page_SaysRefundWhenTheReadingsCostLessThanBilled()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 50m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 50m);

        var vm = await OpenSettlementAsync(contract);

        Assert.Equal("EqWordsRefund 4.90 Currency_Symbol_EUR [en] [en]", vm.ResultText);
        Assert.True(vm.IsRefund);
        Assert.True(vm.Rows[0].IsRefund);
        Assert.Equal(string.Empty, vm.IncompleteText);
    }

    [Fact]
    public async Task Page_MissingMonthLinksToTheReadingsOfThePropertyAtTheEndOfThatMonth()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-09-01", 40m);
        var vm = await OpenSettlementAsync(contract);

        vm.Rows[0].OpenReadingsCommand.Execute(null);
        await Task.Yield();

        var visit = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.ReadingsFor, visit.Route);
        Assert.Equal(_kit.Kit.Property.Id, visit.Parameters![EditorNavigation.PropertyIdParameter]);
        Assert.Equal(new DateOnly(2026, 9, 30), visit.Parameters[ReadingsViewModel.DateParameter]);
    }

    [Fact]
    public async Task Page_EnterSettlementBillOpensASettlementBillForTheCurrentMonthWithTheExpectedAmountAsAHint()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 40m);
        var vm = await OpenSettlementAsync(contract);

        await vm.EnterSettlementBillCommand.ExecuteAsync(null);

        var visit = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillEdit, visit.Route);
        Assert.Equal(contract.Id, visit.Parameters![EditorNavigation.ContractIdParameter]);
        Assert.Equal(contract.PropertyId, visit.Parameters[EditorNavigation.PropertyIdParameter]);
        Assert.Equal("2026-10", visit.Parameters[BillEditViewModel.PeriodParameter]);
        Assert.Equal("Settlement", visit.Parameters[BillEditViewModel.KindParameter]);
        Assert.Equal(5.10m, visit.Parameters[BillEditViewModel.GrossParameter]);
    }

    [Fact]
    public async Task Page_HelpOpensTheEqualizedPaymentsSection()
    {
        var contract = AddEqualizedContract();
        var vm = await OpenSettlementAsync(contract);

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var visit = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.HelpSection, visit.Route);
        Assert.Equal(HelpTopic.EqualizedPayments, visit.Parameters![HelpNavigation.TopicParameter]);
    }

    // ---- Bills ------------------------------------------------------------------------------------------------------

    private async Task<BillEditViewModel> NewBillAsync(Contract contract, BillKind? kind = null)
    {
        var vm = _kit.CreateEditor();
        vm.PresetContractId = contract.Id;
        vm.PresetKind = kind;
        await vm.LoadAsync(null);
        await vm.PendingRefresh;
        return vm;
    }

    [Fact]
    public async Task NewBill_OfAnEqualizedContract_IsEqualizedAndStartsWithThePlanAmountOfItsMonth()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-09-01", 35m, "2026-09-30");
        AddPlan(contract, "2026-10-01", 40m);

        var vm = await NewBillAsync(contract);

        Assert.Equal(BillKind.Equalized, vm.SelectedKind!.Value);
        Assert.Equal(new YearMonth(2026, 10), vm.Period);
        Assert.Equal("40.00", vm.Amounts.GrossText);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task NewBill_TheDefaultFollowsThePeriod_ButATypedTotalIsKept()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-09-01", 35m, "2026-09-30");
        AddPlan(contract, "2026-10-01", 40m);
        var vm = await NewBillAsync(contract);

        vm.PreviousPeriodCommand.Execute(null);
        await vm.PendingRefresh;
        Assert.Equal("35.00", vm.Amounts.GrossText);

        vm.NextPeriodCommand.Execute(null);
        await vm.PendingRefresh;
        Assert.Equal("40.00", vm.Amounts.GrossText);

        vm.Amounts.GrossText = "52.30";
        vm.PreviousPeriodCommand.Execute(null);
        await vm.PendingRefresh;
        Assert.Equal("52.30", vm.Amounts.GrossText);
    }

    [Fact]
    public async Task NewBill_ThePlanAmountIsEditableAndTheVatSplitFollows()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);

        var vm = await NewBillAsync(contract);

        // 40.00 incl. VAT 21 %: net 33.06, VAT 6.94
        Assert.Equal(("33.06", "21", "6.94"), (vm.Amounts.NetText, vm.Amounts.VatPercentText, vm.Amounts.VatText));
    }

    [Fact]
    public async Task NewBill_ComparesWithThePlan_AndShowsTheCalculationAsInformation()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);

        var vm = await NewBillAsync(contract);

        Assert.True(vm.PlanMode);
        Assert.True(vm.ShowComparison);
        Assert.Equal("BillPlanLabel [en]", vm.BasisLabel);
        Assert.Equal("40.00 Currency_Symbol_EUR [en]", vm.CalculatedText);
        Assert.Equal("BillActualByReadings 45.10 Currency_Symbol_EUR [en] [en]", vm.ActualInfoText);
        Assert.True(vm.ShowActualInfo);
        Assert.Equal("0.00 Currency_Symbol_EUR [en] (0.0 %)", vm.DifferenceText);

        vm.Amounts.GrossText = "46.00";
        Assert.StartsWith("+6.00", vm.DifferenceText);
    }

    [Fact]
    public async Task NewBill_WithoutAPlan_IsComparedWithTheCalculationAsBefore()
    {
        var contract = AddEqualizedContract();

        var vm = await NewBillAsync(contract);

        Assert.False(vm.PlanMode);
        Assert.Equal(string.Empty, vm.Amounts.GrossText);
        Assert.Equal("BillCalculatedLabel [en]", vm.BasisLabel);
        Assert.Equal("45.10 Currency_Symbol_EUR [en]", vm.CalculatedText);
        Assert.Equal(string.Empty, vm.ActualInfoText);
    }

    [Fact]
    public async Task NewBill_ForAnotherKind_DoesNotTakeThePlanAmount()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);

        var vm = await NewBillAsync(contract, BillKind.Settlement);

        Assert.Equal(BillKind.Settlement, vm.SelectedKind!.Value);
        Assert.Equal(string.Empty, vm.Amounts.GrossText);
        Assert.False(vm.PlanMode);
    }

    [Fact]
    public async Task NewBill_SwitchingTheKindToEqualizedFillsThePlanAmount_AndBackLeavesItEmpty()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);
        var vm = await NewBillAsync(contract, BillKind.Settlement);

        vm.SelectedKind = vm.Kinds.First(k => k.Value.Equals(BillKind.Equalized));
        Assert.Equal("40.00", vm.Amounts.GrossText);
        Assert.True(vm.PlanMode);

        vm.SelectedKind = vm.Kinds.First(k => k.Value.Equals(BillKind.Settlement));
        Assert.Equal(string.Empty, vm.Amounts.GrossText);
        Assert.False(vm.PlanMode);
    }

    [Fact]
    public async Task NewBill_SavedBillKeepsTheEqualizedKindAndTheTotal()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);
        var vm = await NewBillAsync(contract);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.All);
        Assert.Equal((BillKind.Equalized, 40m, new YearMonth(2026, 10)), (saved.Kind, saved.Gross, saved.Period));
    }

    [Fact]
    public async Task OpenBill_OfAnEqualizedContract_DoesNotOverwriteItsTotalWithThePlan()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);
        var bill = AddEqualizedBill(contract, new YearMonth(2026, 10), 43.20m);

        var vm = _kit.CreateEditor();
        await vm.LoadAsync(bill.Id);
        await vm.PendingRefresh;

        Assert.Equal("43.20", vm.Amounts.GrossText);
        Assert.True(vm.PlanMode);
        Assert.StartsWith("+3.20", vm.DifferenceText);
    }

    // ---- Charges screen ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Charges_CardOfAnEqualizedContractShowsPlanActualAndTheRunningDifference()
    {
        var contract = AddEqualizedContract();
        AddPlan(contract, "2026-10-01", 40m);
        AddEqualizedBill(contract, new YearMonth(2026, 10), 40m);
        var vm = _kit.Kit.CreateCharges();

        await vm.ReloadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.True(card.HasEqualizedText);
        Assert.Equal(
            "ChargesEqualizedLine 40.00 Currency_Symbol_EUR [en]|45.10 Currency_Symbol_EUR [en]|+5.10 Currency_Symbol_EUR [en] [en]",
            card.EqualizedText);
    }

    [Fact]
    public async Task Charges_CardOfAnOrdinaryContractHasNoEqualizedLine()
    {
        _kit.AddWaterContract();
        var vm = _kit.Kit.CreateCharges();

        await vm.ReloadAsync();

        Assert.False(Assert.Single(vm.Cards).HasEqualizedText);
    }

    // ---- Help --------------------------------------------------------------------------------------------------------

    [Fact]
    public void HelpTopics_HaveAnAnchorInBothLanguages()
    {
        Assert.Equal("13-equalized-payments", HelpTopics.GetAnchor(HelpTopic.EqualizedPayments, "en"));
        Assert.Equal("14-property-tax", HelpTopics.GetAnchor(HelpTopic.PropertyTax, "en"));
        Assert.Equal("13-равномерные-платежи", HelpTopics.GetAnchor(HelpTopic.EqualizedPayments, "ru"));
        Assert.Equal("14-налог-на-недвижимость", HelpTopics.GetAnchor(HelpTopic.PropertyTax, "ru"));
    }
}
