using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Taxes;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

// The "Tax by year" card of the contract editor (billing mode Assessed): the yearly amount split into installment bills.
// Like the tariff card it acts on its own: the bills are saved as soon as the dialog is confirmed.
public sealed partial class ContractEditViewModel
{
    private IReadOnlyList<(Bill Bill, BillState State)> _taxBills = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTaxYears), nameof(ShowTaxEmpty))]
    private IReadOnlyList<TaxYearViewModel> _taxYears = [];

    [ObservableProperty]
    private string _taxHeader = string.Empty;

    [ObservableProperty]
    private string _taxHintText = string.Empty;

    [ObservableProperty]
    private string _taxEmptyText = string.Empty;

    [ObservableProperty]
    private string _addTaxYearText = string.Empty;

    [ObservableProperty]
    private string _taxSaveFirstText = string.Empty;

    /// <summary>The card is shown while the billing mode is Assessed (property taxes).</summary>
    public bool ShowTaxCard => SelectedBillingMode?.Value is BillingMode.Assessed;

    public bool HasNoTaxYears => TaxYears.Count == 0;

    public bool ShowTaxEmpty => HasNoTaxYears && !IsNew;

    public bool CanEditTax => !IsNew;

    public bool ShowTaxSaveFirst => IsNew;

    /// <summary>Loads the contract's installment bills with their payment state.</summary>
    private async Task LoadTaxAsync()
    {
        if (IsNew)
        {
            _taxBills = [];
        }
        else
        {
            var rows = new List<(Bill, BillState)>();
            foreach (var bill in (await _billStore.ListByContractAsync(_contract.Id)).Where(b => b.Kind == BillKind.TaxInstallment))
            {
                rows.Add((bill, BillStatus.Of(bill, await _paymentStore.ListByBillAsync(bill.Id), _clock.Today)));
            }

            _taxBills = rows;
        }

        RebuildTax();
    }

    private void RebuildTax()
    {
        var symbol = _localizer["Currency_Symbol_" + _contract.Currency];
        var years = new List<TaxYearViewModel>();
        foreach (var year in _taxBills.Select(r => TaxYearOf(r.Bill)).Distinct().OrderByDescending(y => y))
        {
            var ofYear = TaxInstallmentPlanner.ForYear(_taxBills.Select(r => r.Bill), _contract.Id, year);
            var states = ofYear.ToDictionary(b => b.Id, b => _taxBills.First(r => r.Bill.Id == b.Id).State);
            var rows = ofYear.Select((bill, i) => new TaxInstallmentRowViewModel(
                bill.Id,
                _localizer.Get("TaxInstallmentRow", i + 1, ofYear.Count, bill.DueOn is { } due ? BillText.Date(due) : BillText.Period(bill.Period)),
                BillText.Money(bill.Gross, symbol),
                BillBadge.KindOf(states[bill.Id]),
                BillBadge.TextOf(_localizer, states[bill.Id], symbol),
                () => _ = RunSafeAsync(() => _navigation.GoToAsync(
                    Routes.BillEdit,
                    new Dictionary<string, object> { [EditorNavigation.IdParameter] = bill.Id })))).ToList();
            var total = ofYear.Sum(b => b.Gross);
            var paid = states.Values.Sum(s => s.Paid);
            var open = states.Values.Sum(s => s.Open);
            years.Add(new TaxYearViewModel(
                year,
                _localizer.Get("TaxYearHeader", year, BillText.Money(total, symbol)),
                _localizer.Get("TaxYearSummary", BillText.Money(paid, symbol), BillText.Money(open, symbol)),
                rows,
                ofYear.Any(b => states[b.Id].Open > 0),
                _localizer["TaxPayYear"],
                () => _ = RunSafeAsync(() => PayYearAsync(year))));
        }

        TaxYears = years;
        OnPropertyChanged(nameof(ShowTaxCard));
        OnPropertyChanged(nameof(CanEditTax));
        OnPropertyChanged(nameof(ShowTaxSaveFirst));
        OnPropertyChanged(nameof(ShowTaxEmpty));
        AddTaxYearCommand.NotifyCanExecuteChanged();
    }

    private static int TaxYearOf(Bill bill) => bill.ServiceFrom?.Year ?? bill.Period.Year;

    /// <summary>"Pay whole year": the payment editor with every open installment of the year allocated in full.</summary>
    private Task PayYearAsync(int year)
    {
        var open = TaxInstallmentPlanner.ForYear(_taxBills.Select(r => r.Bill), _contract.Id, year)
            .Where(b => _taxBills.First(r => r.Bill.Id == b.Id).State.Open > 0)
            .Select(b => b.Id)
            .ToList();
        if (open.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _navigation.GoToAsync(
            Routes.PaymentEdit,
            new Dictionary<string, object>
            {
                [EditorNavigation.BillIdsParameter] = string.Join(',', open),
                [EditorNavigation.ContractIdParameter] = _contract.Id,
            });
    }

    /// <summary>
    /// "Enter yearly tax": creates one TaxInstallment bill per installment. For a year that already has installments the user
    /// is asked first; installments with a payment stay, the unpaid ones are removed and the rest of the yearly amount is
    /// split over the remaining due dates.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditTax))]
    private Task AddTaxYearAsync() => RunSafeAsync(async () =>
    {
        var symbol = _localizer["Currency_Symbol_" + _contract.Currency];
        var editor = new TaxYearEditorViewModel(_localizer, _clock, symbol);
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } input)
        {
            return;
        }

        var existing = TaxInstallmentPlanner.ForYear(_taxBills.Select(r => r.Bill), _contract.Id, input.Year);
        var kept = existing.Where(b => _taxBills.First(r => r.Bill.Id == b.Id).State.Paid != 0).ToList();
        var replaced = existing.Except(kept).ToList();
        var plan = TaxInstallmentPlanner.Plan(input.Total, input.DueDates, kept.Count, kept.Sum(b => b.Gross));
        if (existing.Count > 0)
        {
            if (plan.Count == 0)
            {
                await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer.Get("TaxNothingToCreate", input.Year));
                return;
            }

            if (!await Dialogs.ConfirmAsync(
                _localizer["TaxReplaceTitle"], _localizer.Get("TaxReplaceMessage", input.Year), _localizer["TaxReplace"], _localizer["Cancel"]))
            {
                return;
            }
        }
        else if (plan.Count == 0)
        {
            return;
        }

        foreach (var bill in replaced)
        {
            await _billStore.DeleteAsync(bill.Id);
        }

        var created = TaxInstallmentPlanner.CreateBills(
            _contract, input.Year, plan, p => _localizer.Get("TaxInstallmentTitle", input.Year, p.Number, p.Count), _clock);
        foreach (var bill in created)
        {
            await _billStore.SaveAsync(bill);
        }

        _snackbar.Notify(_localizer.Get("TaxCreated", created.Count, input.Year));
        await LoadBillsAsync();
        await LoadTaxAsync();
    });
}
