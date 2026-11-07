using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

// The "Equalized plan" card of the contract editor (billing mode Equalized): the plan versions and the expected settlement.
// Like the tariff card it acts on its own: a plan is saved as soon as its dialog is confirmed.
public sealed partial class ContractEditViewModel
{
    private readonly IEqualizedPlanStore _planStore;
    private readonly EqualizedSettlementService _settlementService;
    private IReadOnlyList<EqualizedPlan> _plans = [];
    private EqualizedReport? _equalizedReport;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPlans), nameof(ShowPlanEmpty))]
    private IReadOnlyList<EqualizedPlanRowViewModel> _planRows = [];

    [ObservableProperty]
    private string _planHeader = string.Empty;

    [ObservableProperty]
    private string _planHintText = string.Empty;

    [ObservableProperty]
    private string _planEmptyText = string.Empty;

    [ObservableProperty]
    private string _addPlanText = string.Empty;

    [ObservableProperty]
    private string _planSaveFirstText = string.Empty;

    [ObservableProperty]
    private string _planSummaryText = string.Empty;

    [ObservableProperty]
    private string _showSettlementText = string.Empty;

    /// <summary>The card is shown while the billing mode is Equalized.</summary>
    public bool ShowEqualizedCard => SelectedBillingMode?.Value is BillingMode.Equalized;

    public bool HasNoPlans => PlanRows.Count == 0;

    public bool ShowPlanEmpty => HasNoPlans && !IsNew;

    /// <summary>Plans belong to a saved contract.</summary>
    public bool CanEditPlans => !IsNew;

    public bool ShowPlanSaveFirst => IsNew;

    /// <summary>The summary and "Show settlement" need a plan and a contract that is saved as an equalized one.</summary>
    public bool ShowSettlementSummary => !IsNew && _plans.Count > 0 && _contract.BillingMode == BillingMode.Equalized;

    /// <summary>Loads the plans and, for a saved equalized contract, the settlement table behind the summary line.</summary>
    private async Task LoadEqualizedAsync()
    {
        if (IsNew)
        {
            _plans = [];
            _equalizedReport = null;
        }
        else if (_contract.BillingMode == BillingMode.Equalized)
        {
            _equalizedReport = await _settlementService.LoadAsync(_contract.Id);
            _plans = _equalizedReport.Plans;
        }
        else
        {
            _equalizedReport = null;
            _plans = await _planStore.ListByContractAsync(_contract.Id);
        }

        RebuildEqualized();
    }

    private void RebuildEqualized()
    {
        var symbol = _localizer["Currency_Symbol_" + _contract.Currency];
        PlanRows =
        [
            .. _plans.OrderByDescending(p => p.ValidFrom).Select(plan => new EqualizedPlanRowViewModel(
                plan.Id,
                plan.ValidTo is { } to
                    ? _localizer.Get("EqPlanRowTo", BillText.Date(plan.ValidFrom), BillText.Date(to), BillText.Money(plan.MonthlyAmount, symbol))
                    : _localizer.Get("EqPlanRow", BillText.Date(plan.ValidFrom), BillText.Money(plan.MonthlyAmount, symbol)),
                plan.Notes ?? string.Empty,
                _localizer["Delete"],
                () => _ = RunSafeAsync(() => EditPlanAsync(plan)),
                () => _ = RunSafeAsync(() => RemovePlanAsync(plan)))),
        ];
        PlanSummaryText = SummaryOf(_equalizedReport, symbol);
        OnPropertyChanged(nameof(ShowEqualizedCard));
        OnPropertyChanged(nameof(CanEditPlans));
        OnPropertyChanged(nameof(ShowPlanSaveFirst));
        OnPropertyChanged(nameof(ShowPlanEmpty));
        OnPropertyChanged(nameof(ShowSettlementSummary));
        ShowSettlementCommand.NotifyCanExecuteChanged();
        AddPlanCommand.NotifyCanExecuteChanged();
    }

    /// <summary>"Expected settlement: +9,50 € (surcharge) for 06.2026–11.2026", plus a note when months are missing.</summary>
    private string SummaryOf(EqualizedReport? report, string symbol)
    {
        if (report is null || report.Plans.Count == 0)
        {
            return string.Empty;
        }

        var result = report.Settlement;
        var key = result.Expected switch
        {
            > 0 => "EqSummarySurcharge",
            < 0 => "EqSummaryRefund",
            _ => "EqSummaryZero",
        };
        var text = _localizer.Get(
            key, BillText.Money(Math.Abs(result.Expected), symbol), BillText.Period(result.From), BillText.Period(result.To));
        return result.IsComplete
            ? text
            : text + " " + _localizer.Get("EqSummaryIncomplete", result.MissingMonths.ToString(CultureInfo.CurrentCulture));
    }

    [RelayCommand(CanExecute = nameof(CanEditPlans))]
    private Task AddPlanAsync() => RunSafeAsync(async () =>
    {
        var editor = new EqualizedPlanEditorViewModel(_localizer, _clock, null, candidate => EqualizedPlans.ValidateWith(_plans, candidate));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        result.ContractId = _contract.Id;
        await SavePlanAsync(result);
    });

    private async Task EditPlanAsync(EqualizedPlan plan)
    {
        var editor = new EqualizedPlanEditorViewModel(_localizer, _clock, plan, candidate => EqualizedPlans.ValidateWith(_plans, candidate));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        await SavePlanAsync(result);
    }

    private async Task RemovePlanAsync(EqualizedPlan plan)
    {
        var symbol = _localizer["Currency_Symbol_" + _contract.Currency];
        var text = plan.ValidTo is { } to
            ? _localizer.Get("EqPlanRowTo", BillText.Date(plan.ValidFrom), BillText.Date(to), BillText.Money(plan.MonthlyAmount, symbol))
            : _localizer.Get("EqPlanRow", BillText.Date(plan.ValidFrom), BillText.Money(plan.MonthlyAmount, symbol));
        if (!await ConfirmRowDeleteAsync(text))
        {
            return;
        }

        await _planStore.DeleteAsync(plan.Id);
        await LoadEqualizedAsync();
    }

    /// <summary>Saves the plan; a plan that runs into the new one ends the day before it starts.</summary>
    private async Task SavePlanAsync(EqualizedPlan plan)
    {
        foreach (var predecessor in EqualizedPlans.PredecessorsToClose(_plans, plan))
        {
            predecessor.ValidTo = plan.ValidFrom.AddDays(-1);
            await _planStore.SaveAsync(predecessor);
        }

        await _planStore.SaveAsync(plan);
        await LoadEqualizedAsync();
    }

    [RelayCommand(CanExecute = nameof(CanShowSettlement))]
    private Task ShowSettlementAsync() => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.EqualizedSettlement,
        new Dictionary<string, object> { [EditorNavigation.ContractIdParameter] = _contract.Id }));

    private bool CanShowSettlement() => ShowSettlementSummary;
}
