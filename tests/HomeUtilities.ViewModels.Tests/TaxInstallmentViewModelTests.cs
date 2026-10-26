using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// Property tax installments: the "Enter yearly tax" dialog and the "Tax by year" card of the contract editor. Today is
/// 27 Oct 2026. FakeLocalizer renders texts as "key [en]".
/// </summary>
public class TaxInstallmentViewModelTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public TaxInstallmentViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private Contract AddTaxContract()
    {
        var supplier = new Supplier { Name = "Municipality" };
        _kit.Kit.Suppliers.Stored.Add(supplier);
        var contract = new Contract
        {
            PropertyId = _kit.Kit.Property.Id,
            SupplierId = supplier.Id,
            Title = "Property tax",
            Service = ServiceKind.PropertyTaxBuilding,
            CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.PropertyTaxBuilding),
            BillingMode = BillingMode.Assessed,
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Kit.Contracts.Stored.Add(contract);
        return contract;
    }

    private async Task<ContractEditViewModel> OpenAsync(Contract contract)
    {
        var vm = _kit.Kit.CreateContractEditor();
        await vm.LoadAsync(contract.PropertyId, contract.Id);
        return vm;
    }

    private void Script(string year, string amount, Action<TaxYearEditorViewModel>? adjust = null) =>
        _kit.Kit.Dialogs.EditorScript = e =>
        {
            var dialog = (TaxYearEditorViewModel)e;
            dialog.YearText = year;
            dialog.AmountText = amount;
            adjust?.Invoke(dialog);
            dialog.SaveCommand.Execute(null);
        };

    private List<Bill> Installments(Contract contract) =>
        [.. _kit.Bills.Stored.Where(b => b.ContractId == contract.Id && b.Kind == BillKind.TaxInstallment).OrderBy(b => b.DueOn)];

    private static EnumOption Count(TaxYearEditorViewModel dialog, int count) => dialog.Counts.First(c => c.Value.Equals(count));

    // ---- The dialog ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Dialog_StartsWithThisYearFourInstallmentsAndTheLatvianDates()
    {
        var dialog = new TaxYearEditorViewModel(_kit.Kit.Localizer, _kit.Kit.Clock, "€");

        Assert.Equal("2026", dialog.YearText);
        Assert.Equal([1, 2, 4], dialog.Counts.Select(c => (int)c.Value));
        Assert.Equal(4, dialog.SelectedCount!.Value);
        Assert.Equal(
            [new DateOnly(2026, 3, 31), new DateOnly(2026, 5, 15), new DateOnly(2026, 8, 15), new DateOnly(2026, 11, 15)],
            dialog.DueDates.Select(d => d.Date));
        Assert.Equal(["TaxYearInstallmentNumber 1 [en]", "TaxYearInstallmentNumber 2 [en]", "TaxYearInstallmentNumber 3 [en]", "TaxYearInstallmentNumber 4 [en]"], dialog.DueDates.Select(d => d.Label));
    }

    [Fact]
    public void Dialog_ChangingTheCountOrTheYearPutsTheDefaultDatesBack()
    {
        var dialog = new TaxYearEditorViewModel(_kit.Kit.Localizer, _kit.Kit.Clock, "€");

        dialog.SelectedCount = Count(dialog, 2);
        Assert.Equal([new DateOnly(2026, 3, 31), new DateOnly(2026, 8, 15)], dialog.DueDates.Select(d => d.Date));

        dialog.SelectedCount = Count(dialog, 1);
        Assert.Equal([new DateOnly(2026, 3, 31)], dialog.DueDates.Select(d => d.Date));

        dialog.DueDates[0].Date = new DateOnly(2026, 4, 5);
        dialog.YearText = "2028";
        Assert.Equal([new DateOnly(2028, 3, 31)], dialog.DueDates.Select(d => d.Date));
        Assert.Equal(0, dialog.SelectedCountIndex);
    }

    [Fact]
    public void Dialog_PreviewShowsTheSplitWithTheRemainderOnTheFirstInstallment()
    {
        var dialog = new TaxYearEditorViewModel(_kit.Kit.Localizer, _kit.Kit.Clock, "€");

        dialog.AmountText = "101.00";
        Assert.Equal("25.25 € · 25.25 € · 25.25 € · 25.25 €", dialog.PreviewText);

        dialog.AmountText = "100.03";
        Assert.Equal("25.03 € · 25.00 € · 25.00 € · 25.00 €", dialog.PreviewText);

        dialog.AmountText = "abc";
        Assert.Equal(string.Empty, dialog.PreviewText);
    }

    [Fact]
    public void Dialog_ChecksYearAmountAndDates_AndReturnsTheInputWhenValid()
    {
        var dialog = new TaxYearEditorViewModel(_kit.Kit.Localizer, _kit.Kit.Clock, "€");

        dialog.YearText = "20x6";
        dialog.AmountText = "-5";
        dialog.SaveCommand.Execute(null);
        Assert.False(dialog.Saved);
        Assert.NotEmpty(dialog.YearError);

        dialog.YearText = "2026";
        dialog.SaveCommand.Execute(null);
        Assert.Equal("Error_tax_yearly_amount_not_positive [en]", dialog.AmountError);

        dialog.AmountText = "101,00".Replace(',', '.');
        dialog.DueDates[1].Date = new DateOnly(2027, 1, 10);
        dialog.DueDates[3].Date = new DateOnly(2026, 8, 1);
        dialog.SaveCommand.Execute(null);
        Assert.False(dialog.Saved);
        Assert.Equal("Error_tax_due_date_outside_year 2026 [en]", dialog.DueDates[1].Error);
        Assert.Equal("Error_tax_due_dates_not_ascending [en]", dialog.DueDates[3].Error);

        dialog.DueDates[1].Date = new DateOnly(2026, 5, 15);
        dialog.DueDates[3].Date = new DateOnly(2026, 11, 15);
        dialog.SaveCommand.Execute(null);
        Assert.True(dialog.Saved);
        Assert.Equal((2026, 101m, 4), (dialog.Result!.Year, dialog.Result.Total, dialog.Result.DueDates.Count));
        Assert.All(dialog.DueDates, d => Assert.Equal(string.Empty, d.Error));
    }

    // ---- The card -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Card_IsShownForTheAssessedModeOnly()
    {
        var vm = await OpenAsync(AddTaxContract());

        Assert.True(vm.ShowTaxCard);
        Assert.False(vm.ShowEqualizedCard);
        Assert.True(vm.ShowTaxEmpty);
        Assert.Empty(vm.TaxYears);
        Assert.Equal(HelpTopic.PropertyTax, await HelpTopicOfAsync(vm));

        vm.SelectedBillingMode = vm.BillingModes.First(m => m.Value.Equals(BillingMode.Equalized));
        Assert.Equal(HelpTopic.EqualizedPayments, await HelpTopicOfAsync(vm));

        vm.SelectedBillingMode = vm.BillingModes.First(m => m.Value.Equals(BillingMode.Actual));
        Assert.False(vm.ShowTaxCard);
        Assert.Equal(HelpTopic.Contracts, await HelpTopicOfAsync(vm));
    }

    private async Task<object> HelpTopicOfAsync(ContractEditViewModel vm)
    {
        await vm.OpenHelpCommand.ExecuteAsync(null);
        return _kit.Kit.Navigation.Visited.Last().Parameters![HelpNavigation.TopicParameter];
    }

    [Fact]
    public async Task EnterYearlyTax_CreatesOneInstallmentBillPerDueDate()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "101.00");

        await vm.AddTaxYearCommand.ExecuteAsync(null);

        // 101,00 / 4 = 25,25 x 4
        var bills = Installments(contract);
        Assert.Equal(4, bills.Count);
        Assert.Equal(
            [new DateOnly(2027, 3, 31), new DateOnly(2027, 5, 15), new DateOnly(2027, 8, 15), new DateOnly(2027, 11, 15)],
            bills.Select(b => b.DueOn!.Value));
        Assert.All(bills, b => Assert.Equal(25.25m, b.Gross));
        Assert.Equal(
            [new YearMonth(2027, 3), new YearMonth(2027, 5), new YearMonth(2027, 8), new YearMonth(2027, 11)],
            bills.Select(b => b.Period));
        Assert.All(bills, b => Assert.Equal((new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)), (b.ServiceFrom!.Value, b.ServiceTo!.Value)));
        Assert.Equal("TaxInstallmentTitle 2027|2|4 [en]", bills[1].Title);
        Assert.All(bills, b => Assert.Equal((contract.Id, contract.PropertyId, contract.CategoryId, contract.SupplierId), (b.ContractId, b.PropertyId, b.CategoryId, b.SupplierId)));
        Assert.All(bills, b => Assert.True(BillValidator.Validate(b, contract).IsValid));

        var year = Assert.Single(vm.TaxYears);
        Assert.Equal(2027, year.Year);
        Assert.Equal($"TaxYearHeader 2027|101.00 {Eur} [en]", year.HeaderText);
        Assert.Equal($"TaxYearSummary 0.00 {Eur}|101.00 {Eur} [en]", year.SummaryText);
        Assert.Equal(4, year.Installments.Count);
        Assert.Equal($"TaxInstallmentRow 2|4|15.05.2027 [en]", year.Installments[1].TitleText);
        Assert.True(year.CanPayYear);
        Assert.False(vm.ShowTaxEmpty);
        Assert.Single(_kit.Kit.Toast.Messages);
    }

    [Fact]
    public async Task EnterYearlyTax_AcceptsChangedDatesAndAnotherCount()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "100.03", d =>
        {
            d.SelectedCount = Count(d, 2);
            d.DueDates[1].Date = new DateOnly(2027, 9, 1);
        });

        await vm.AddTaxYearCommand.ExecuteAsync(null);

        var bills = Installments(contract);
        Assert.Equal([new DateOnly(2027, 3, 31), new DateOnly(2027, 9, 1)], bills.Select(b => b.DueOn!.Value));
        Assert.Equal([50.02m, 50.01m], bills.Select(b => b.Gross));
        Assert.Equal("TaxInstallmentTitle 2027|1|2 [en]", bills[0].Title);
    }

    [Fact]
    public async Task EnterYearlyTax_CancelledDialogCreatesNothing()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);

        await vm.AddTaxYearCommand.ExecuteAsync(null);

        Assert.Empty(Installments(contract));
        Assert.Empty(_kit.Kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task EnterYearlyTax_AgainAsksAndReplacesTheUnpaidOnesOnly()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "101.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);
        var first = Installments(contract);
        _kit.AddPayment(25.25m, (first[0], 25.25m));
        await vm.OnAppearingAsync();

        // reassessed to 120,00: the paid installment (25,25) stays, 94,75 is split over the three open dates: 31,59 + 31,58 + 31,58
        Script("2027", "120.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);

        var confirmation = Assert.Single(_kit.Kit.Dialogs.Confirmations);
        Assert.Equal("TaxReplaceTitle [en]", confirmation.Title);
        Assert.Equal("TaxReplaceMessage 2027 [en]", confirmation.Message);
        Assert.Equal(first.Skip(1).Select(b => b.Id), _kit.Bills.Deleted);
        var now = Installments(contract);
        Assert.Equal(4, now.Count);
        Assert.Equal(first[0].Id, now[0].Id);
        Assert.Equal([25.25m, 31.59m, 31.58m, 31.58m], now.Select(b => b.Gross));
        Assert.Equal(120m, now.Sum(b => b.Gross));
        Assert.Equal(
            ["TaxInstallmentTitle 2027|1|4 [en]", "TaxInstallmentTitle 2027|2|4 [en]", "TaxInstallmentTitle 2027|3|4 [en]", "TaxInstallmentTitle 2027|4|4 [en]"],
            now.Select(b => b.Title));
        Assert.Equal(now.Select(b => b.DueOn), now.OrderBy(b => b.DueOn).Select(b => b.DueOn));
        Assert.Single(vm.TaxYears);
    }

    [Fact]
    public async Task EnterYearlyTax_AgainWithoutConfirmationChangesNothing()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "101.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);
        var before = Installments(contract).Select(b => b.Id).ToList();

        _kit.Kit.Dialogs.ConfirmResult = false;
        Script("2027", "200.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);

        Assert.Equal(before, Installments(contract).Select(b => b.Id));
        Assert.Empty(_kit.Bills.Deleted);
    }

    [Fact]
    public async Task EnterYearlyTax_AgainWhenEverythingIsPaidSaysThereIsNothingToCreate()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "100.00", d => d.SelectedCount = Count(d, 2));
        await vm.AddTaxYearCommand.ExecuteAsync(null);
        var bills = Installments(contract);
        _kit.AddPayment(100m, (bills[0], 50m), (bills[1], 50m));
        await vm.OnAppearingAsync();

        Script("2027", "100.00", d => d.SelectedCount = Count(d, 2));
        await vm.AddTaxYearCommand.ExecuteAsync(null);

        var alert = Assert.Single(_kit.Kit.Dialogs.Alerts);
        Assert.Equal("TaxNothingToCreate 2027 [en]", alert.Message);
        Assert.Empty(_kit.Kit.Dialogs.Confirmations);
        Assert.Empty(_kit.Bills.Deleted);
        Assert.Equal(2, Installments(contract).Count);
    }

    [Fact]
    public async Task Card_ShowsPaidAndRemaining_AndAnInstallmentPastItsDueDateIsOverdue()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2026", "100.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);
        var bills = Installments(contract);
        _kit.AddPayment(25m, (bills[0], 25m));
        await vm.OnAppearingAsync();

        var year = Assert.Single(vm.TaxYears);
        Assert.Equal($"TaxYearSummary 25.00 {Eur}|75.00 {Eur} [en]", year.SummaryText);
        Assert.Equal(
            [BillBadgeKind.Paid, BillBadgeKind.Overdue, BillBadgeKind.Overdue, BillBadgeKind.Unpaid],
            year.Installments.Select(i => i.Badge));
        Assert.True(year.Installments[0].IsBadgePaid);
        Assert.True(year.Installments[1].IsBadgeOverdue);
    }

    [Fact]
    public async Task Card_ListsTheYearsNewestFirst()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        foreach (var year in new[] { "2026", "2027", "2025" })
        {
            Script(year, "40.00", d => d.SelectedCount = Count(d, 1));
            await vm.AddTaxYearCommand.ExecuteAsync(null);
        }

        Assert.Equal([2027, 2026, 2025], vm.TaxYears.Select(y => y.Year));
    }

    [Fact]
    public async Task PayWholeYear_OpensThePaymentEditorWithEveryOpenInstallmentOfTheYear()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "100.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);
        var bills = Installments(contract);
        _kit.AddPayment(25m, (bills[0], 25m));
        await vm.OnAppearingAsync();
        _kit.Kit.Navigation.Visited.Clear();

        vm.TaxYears[0].PayYearCommand.Execute(null);
        await Task.Yield();

        var visit = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.PaymentEdit, visit.Route);
        Assert.Equal(string.Join(',', bills.Skip(1).Select(b => b.Id)), visit.Parameters![EditorNavigation.BillIdsParameter]);
        Assert.Equal(contract.Id, visit.Parameters[EditorNavigation.ContractIdParameter]);
    }

    [Fact]
    public async Task PaymentEditor_StartsWithAllPresetBillsAllocatedInFull()
    {
        var contract = AddTaxContract();
        var vm = await OpenAsync(contract);
        Script("2027", "101.00");
        await vm.AddTaxYearCommand.ExecuteAsync(null);
        var bills = Installments(contract);
        _kit.AddPayment(10m, (bills[0], 10m));

        var editor = _kit.CreatePaymentEditor();
        editor.PresetBillIds = [.. bills.Select(b => b.Id)];
        editor.PresetContractId = contract.Id;
        await editor.LoadAsync(null);

        // 101,00 - 10,00 already paid on the first installment = 91,00 open over four bills
        Assert.Equal("91.00", editor.AmountText);
        Assert.Equal(4, editor.AllocationRows.Count);
        Assert.Equal("Property tax", editor.SelectedContractName);
        Assert.False(editor.IsDirty);

        editor.Reference = "tax";
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Payments.Saved);
        Assert.Equal(91m, saved.Amount);
        Assert.Equal([15.25m, 25.25m, 25.25m, 25.25m], saved.Allocations.Select(a => a.Amount));
        Assert.All(bills, b => Assert.True(BillStatus.Of(b, _kit.Payments.Stored, Today).IsPaid));
    }
}
