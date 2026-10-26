using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The settlement page of an equalized contract: month by month the plan, what was billed, what the readings cost, the
/// difference and the running total, and in words what the yearly settlement will probably be. "Enter settlement bill" opens
/// a new Settlement bill with the expected amount as a hint.
/// </summary>
public sealed partial class EqualizedSettlementViewModel : ViewModelBase
{
    private readonly EqualizedSettlementService _service;
    private readonly IContractStore _contractStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;

    private Contract? _contract;
    private EqualizedReport? _report;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows), nameof(IsEmpty))]
    private IReadOnlyList<EqualizedSettlementRowViewModel> _rows = [];

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _isSurcharge;

    [ObservableProperty]
    private bool _isRefund;

    [ObservableProperty]
    private string _periodText = string.Empty;

    [ObservableProperty]
    private string _totalsText = string.Empty;

    [ObservableProperty]
    private string _incompleteText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    private string _monthHeader = string.Empty;

    [ObservableProperty]
    private string _billedHeader = string.Empty;

    [ObservableProperty]
    private string _actualHeader = string.Empty;

    [ObservableProperty]
    private string _differenceHeader = string.Empty;

    [ObservableProperty]
    private string _runningHeader = string.Empty;

    [ObservableProperty]
    private string _enterBillText = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _hintText = string.Empty;

    public EqualizedSettlementViewModel(
        EqualizedSettlementService service,
        IContractStore contractStore,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation)
    {
        _service = service;
        _contractStore = contractStore;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        RefreshTexts();
    }

    public Guid ContractId { get; private set; }

    public bool HasRows => Rows.Count > 0;

    public bool IsEmpty => Rows.Count == 0;

    public bool HasIncompleteText => IncompleteText.Length > 0;

    /// <summary>Loads the settlement of the contract.</summary>
    public Task LoadAsync(Guid contractId)
    {
        ContractId = contractId;
        return RunSafeAsync(ReloadAsync);
    }

    /// <summary>Back on the page (from the readings or the bill): everything may have changed.</summary>
    public override Task OnAppearingAsync() => ContractId == Guid.Empty ? Task.CompletedTask : RunSafeAsync(ReloadAsync);

    public async Task ReloadAsync()
    {
        _contract = await _contractStore.GetAsync(ContractId);
        _report = _contract is null ? null : await _service.LoadAsync(ContractId);
        Rebuild();
    }

    protected override void RefreshTexts()
    {
        HelpText = _localizer["SectionHelp"];
        MonthHeader = _localizer["EqColMonth"];
        BilledHeader = _localizer["EqColBilled"];
        ActualHeader = _localizer["EqColActual"];
        DifferenceHeader = _localizer["EqColDifference"];
        RunningHeader = _localizer["EqColRunning"];
        EnterBillText = _localizer["EqEnterSettlementBill"];
        EmptyText = _localizer["EqSettlementEmpty"];
        HintText = _localizer["EqSettlementHint"];
        if (_report is not null)
        {
            Rebuild();
        }
        else
        {
            Title = _localizer["EqSettlementTitle"];
        }
    }

    private void Rebuild()
    {
        if (_contract is null || _report is null)
        {
            Rows = [];
            return;
        }

        var symbol = _localizer["Currency_Symbol_" + _contract.Currency];
        var result = _report.Settlement;
        Title = _localizer.Get("EqSettlementPageTitle", _contract.Title);
        PeriodText = _localizer.Get("EqSettlementPeriod", BillText.Period(result.From), BillText.Period(result.To));
        Rows =
        [
            .. result.Months.Select(m => new EqualizedSettlementRowViewModel(
                BillText.Period(m.Period),
                m.PlanAmount is { } plan ? _localizer.Get("EqPlanShort", BillText.Money(plan, symbol)) : string.Empty,
                BillText.Money(m.Billed, symbol),
                m.Actual is { } actual ? BillText.Money(actual, symbol) : "–",
                m.Difference is { } difference ? BillText.SignedMoney(difference, symbol) : "–",
                m.IsComplete ? BillText.SignedMoney(m.RunningTotal, symbol) : "–",
                m.IsComplete,
                m.Difference,
                m.IsComplete ? string.Empty : _localizer["EqReadingsMissing"],
                () => _ = RunSafeAsync(() => OpenReadingsAsync(m.Period)))),
        ];

        TotalsText = _localizer.Get(
            "EqTotals", BillText.Money(result.TotalBilled, symbol), BillText.Money(result.TotalActual, symbol));
        var amount = BillText.Money(Math.Abs(result.Expected), symbol);
        ResultText = result.Expected switch
        {
            > 0 => _localizer.Get("EqWordsSurcharge", amount),
            < 0 => _localizer.Get("EqWordsRefund", amount),
            _ => _localizer["EqWordsZero"],
        };
        IsSurcharge = result.Expected > 0;
        IsRefund = result.Expected < 0;
        IncompleteText = result.IsComplete
            ? string.Empty
            : _localizer.Get("EqIncomplete", result.MissingMonths.ToString(CultureInfo.CurrentCulture));
        OnPropertyChanged(nameof(HasIncompleteText));
    }

    /// <summary>The readings of the contract's property, dated the last day of the month (today if that is still ahead).</summary>
    private Task OpenReadingsAsync(YearMonth month)
    {
        if (_contract is null)
        {
            return Task.CompletedTask;
        }

        var date = month.LastDay > _clock.Today ? _clock.Today : month.LastDay;
        return _navigation.GoToAsync(
            Routes.ReadingsFor,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
                [ReadingsViewModel.DateParameter] = date,
            });
    }

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, HelpTopic.EqualizedPayments));

    /// <summary>A new Settlement bill for the current month; the expected amount is only a hint in the empty Total field.</summary>
    [RelayCommand]
    private Task EnterSettlementBillAsync() => RunSafeAsync(() =>
    {
        if (_contract is null || _report is null)
        {
            return Task.CompletedTask;
        }

        var parameters = new Dictionary<string, object>
        {
            [EditorNavigation.ContractIdParameter] = _contract.Id,
            [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
            [BillEditViewModel.PeriodParameter] = YearMonth.From(_clock.Today).ToString(),
            [BillEditViewModel.KindParameter] = nameof(Core.Billing.BillKind.Settlement),
        };
        if (_report.Settlement.Expected != 0)
        {
            parameters[BillEditViewModel.GrossParameter] = _report.Settlement.Expected;
        }

        return _navigation.GoToAsync(Routes.BillEdit, parameters);
    });
}
