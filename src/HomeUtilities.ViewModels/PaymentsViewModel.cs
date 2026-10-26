using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The Payments screen: the payments filtered by property, period (all years, year or month) and contract with what each one pays and its prepayment, and
/// the totals of the list. Two-pane like Bills; the editor is <see cref="PaymentEditViewModel"/>.
/// </summary>
public sealed partial class PaymentsViewModel : MasterDetailViewModel<PaymentEditViewModel>
{
    /// <summary>How many bills a row names before it says "+N".</summary>
    public const int BillsNamed = 2;

    protected override HelpTopic GuideTopic => HelpTopic.Payments;

    private readonly IPaymentStore _paymentStore;
    private readonly IBillStore _billStore;
    private readonly IContractStore _contractStore;
    private readonly IPropertyStore _propertyStore;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _localizer;
    private readonly PaymentFilterState _filter;

    private IReadOnlyList<Payment> _payments = [];
    private IReadOnlyList<Property> _properties = [];
    private Dictionary<Guid, Bill> _billsById = [];
    private Dictionary<Guid, Contract> _contracts = [];
    private Guid? _highlightContractId;

    [ObservableProperty]
    private IReadOnlyList<PaymentItemViewModel> _items = [];

    [ObservableProperty]
    private PaymentsListTexts _texts;

    [ObservableProperty]
    private string _propertyFilterName = string.Empty;

    [ObservableProperty]
    private string _contractFilterName = string.Empty;

    [ObservableProperty]
    private string _totalsText = string.Empty;

    [ObservableProperty]
    private string _nothingFoundText = string.Empty;

    [ObservableProperty]
    private bool _showNothingFound;

    public PaymentsViewModel(
        IPaymentStore paymentStore,
        IBillStore billStore,
        IContractStore contractStore,
        IPropertyStore propertyStore,
        IDialogService dialogs,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        ISectionSelectionMemory selectionMemory,
        PaymentFilterState filter,
        Func<PaymentEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.PaymentEdit, selectionMemory)
    {
        _paymentStore = paymentStore;
        _billStore = billStore;
        _contractStore = contractStore;
        _propertyStore = propertyStore;
        _dialogs = dialogs;
        _localizer = localizer;
        _filter = filter;
        _texts = new PaymentsListTexts(localizer);
        Period = new PeriodFilterViewModel(() => _filter.Period, p => _filter.Period = p, clock, localizer, RebuildItems);
        RefreshTexts();
    }

    /// <summary>The period filter (all years / year / month) on the payment date; the current year until the user changes it.</summary>
    public PeriodFilterViewModel Period { get; }

    public Guid? PropertyFilterId => _filter.PropertyId;

    public Guid? ContractFilterId => _filter.ContractId;

    protected override int TotalCount => _payments.Count;

    public override async Task ReloadAsync()
    {
        _payments = await _paymentStore.ListAsync();
        _properties = await _propertyStore.ListAsync(true);
        _contracts = (await _contractStore.ListAsync()).ToDictionary(c => c.Id);
        _billsById = (await _billStore.ListAsync()).ToDictionary(b => b.Id);

        if (_filter.PropertyId is { } property && _properties.All(p => p.Id != property))
        {
            _filter.PropertyId = null;
        }

        if (_filter.ContractId is { } contract && !_contracts.ContainsKey(contract))
        {
            _filter.ContractId = null;
        }

        IsLoaded = true;
        RebuildItems();
        RaiseItemsChanged();
    }

    /// <summary>Opens the screen for a contract ("All payments"): its property, every year, the contract's payments highlighted.</summary>
    public Task OpenForAsync(Guid propertyId, Guid? contractId) => RunSafeAsync(async () =>
    {
        _filter.PropertyId = propertyId;
        _filter.ContractId = contractId;
        Period.Set(PeriodFilter.ForAllYears(Period.Current.Year));
        _highlightContractId = contractId;
        if (IsLoaded)
        {
            await ReloadAsync();
        }
    });

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionPayments"];
        EmptyText = _localizer["PaymentsEmpty"];
        AddText = _localizer["PaymentAdd"];
        HelpText = _localizer["SectionHelp"];
        SelectHintText = _localizer["PaymentSelectHint"];
        NothingFoundText = _localizer["PaymentsNothingFound"];
        Texts = new PaymentsListTexts(_localizer);
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _payments.Any(p => p.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedEditor is { IsNew: false } editor && editor.EntityId == item.Id;
        }
    }

    // ---- Filters --------------------------------------------------------------------------------------------------

    [RelayCommand]
    private Task ChoosePropertyFilterAsync() => RunSafeAsync(async () =>
    {
        var all = _localizer["PaymentsAllProperties"];
        var picked = await _dialogs.ChooseAsync(_localizer["PaymentsPropertyFilter"], [all, .. _properties.Select(p => p.Name)]);
        if (picked is null)
        {
            return;
        }

        _filter.PropertyId = picked == all ? null : _properties.FirstOrDefault(p => p.Name == picked)?.Id;
        if (_filter.PropertyId is { } property && _filter.ContractId is { } contract
            && _contracts.TryGetValue(contract, out var current) && current.PropertyId != property)
        {
            _filter.ContractId = null;
        }

        _highlightContractId = null;
        RebuildItems();
    });

    [RelayCommand]
    private Task ChooseContractFilterAsync() => RunSafeAsync(async () =>
    {
        var all = _localizer["PaymentsAllContracts"];
        var options = ContractFilterOptions();
        var picked = await _dialogs.ChooseAsync(_localizer["PaymentsContractFilter"], [all, .. options.Select(o => o.Name)]);
        if (picked is null)
        {
            return;
        }

        _filter.ContractId = picked == all ? null : options.FirstOrDefault(o => o.Name == picked)?.Id;
        _highlightContractId = null;
        RebuildItems();
    });

    /// <summary>The contracts of the chosen property; with all properties, every contract (named with its property when there are several).</summary>
    private List<ChoiceOption> ContractFilterOptions()
    {
        var withProperty = _filter.PropertyId is null && _properties.Count > 1;
        return
        [
            .. _contracts.Values
                .Where(c => _filter.PropertyId is not { } property || c.PropertyId == property)
                .OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(c => new ChoiceOption(
                    c.Id,
                    withProperty ? $"{c.Title} · {_properties.FirstOrDefault(p => p.Id == c.PropertyId)?.Name}" : c.Title,
                    string.Empty)),
        ];
    }

    // ---- Rows and totals ------------------------------------------------------------------------------------------

    private void RebuildItems()
    {
        var rows = _payments.Where(Matches).ToList();
        Items =
        [
            .. rows
                .OrderByDescending(p => p.PaidOn)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new PaymentItemViewModel(
                    p.Id,
                    p.Amount < 0,
                    BillText.Date(p.PaidOn),
                    PaymentText.Amount(_localizer, p.Amount, SymbolOf(p)),
                    TitleOf(p),
                    EnumText.Get(_localizer, p.Method),
                    PaysOf(p),
                    PaymentRules.Unallocated(p) is var unallocated && unallocated > 0
                        ? _localizer.Get("PaymentPrepaymentBadge", BillText.Money(unallocated, SymbolOf(p)))
                        : string.Empty,
                    p.CreatedByQuickAction ? _localizer["PaymentQuickMarker"] : string.Empty,
                    _highlightContractId is { } highlighted && ContractIdsOf(p).Contains(highlighted),
                    id => SelectCommand.Execute(id))),
        ];

        PropertyFilterName = _filter.PropertyId is { } id
            ? _properties.FirstOrDefault(p => p.Id == id)?.Name ?? _localizer["PaymentsAllProperties"]
            : _localizer["PaymentsAllProperties"];
        ContractFilterName = _filter.ContractId is { } contractId && _contracts.TryGetValue(contractId, out var contract)
            ? contract.Title
            : _localizer["PaymentsAllContracts"];
        Period.Refresh();
        TotalsText = BuildTotals(rows);
        ShowNothingFound = IsLoaded && _payments.Count > 0 && Items.Count == 0;
        MarkSelectedItem();
    }

    private bool Matches(Payment payment)
    {
        if (!Period.Current.Contains(payment.PaidOn))
        {
            return false;
        }

        if (_filter.PropertyId is { } property && !PropertyIdsOf(payment).Contains(property))
        {
            return false;
        }

        return _filter.ContractId is not { } contract || ContractIdsOf(payment).Contains(contract);
    }

    /// <summary>The properties a payment belongs to: its own, its contract's and those of the bills it settles.</summary>
    private HashSet<Guid> PropertyIdsOf(Payment payment)
    {
        var result = new HashSet<Guid>();
        if (payment.PropertyId is { } own)
        {
            result.Add(own);
        }

        if (payment.ContractId is { } contractId && _contracts.TryGetValue(contractId, out var contract))
        {
            result.Add(contract.PropertyId);
        }

        foreach (var bill in BillsOf(payment))
        {
            result.Add(bill.PropertyId);
        }

        return result;
    }

    /// <summary>The contracts a payment belongs to: its own and those of the bills it settles.</summary>
    private HashSet<Guid> ContractIdsOf(Payment payment)
    {
        var result = new HashSet<Guid>();
        if (payment.ContractId is { } own)
        {
            result.Add(own);
        }

        foreach (var bill in BillsOf(payment))
        {
            if (bill.ContractId is { } contractId)
            {
                result.Add(contractId);
            }
        }

        return result;
    }

    private IEnumerable<Bill> BillsOf(Payment payment) =>
        payment.Allocations
            .Where(a => !a.IsDeleted && _billsById.ContainsKey(a.BillId))
            .Select(a => _billsById[a.BillId]);

    /// <summary>The contract's title; for a payment without a contract the property, or "Several contracts" when it settles bills of several.</summary>
    private string TitleOf(Payment payment)
    {
        if (payment.ContractId is { } contractId && _contracts.TryGetValue(contractId, out var contract))
        {
            return contract.Title;
        }

        if (BillsOf(payment).Select(b => b.ContractId).Distinct().Count() > 1)
        {
            return _localizer["PaymentSeveralContracts"];
        }

        var propertyId = payment.PropertyId ?? BillsOf(payment).Select(b => (Guid?)b.PropertyId).FirstOrDefault();
        return _properties.FirstOrDefault(p => p.Id == propertyId)?.Name ?? string.Empty;
    }

    /// <summary>"Water 09.2026, Water 10.2026 +1": the first <see cref="BillsNamed"/> bills by period, then "+N".</summary>
    private string PaysOf(Payment payment)
    {
        var bills = BillsOf(payment)
            .OrderBy(b => b.Period)
            .ThenBy(b => b.CreatedAt)
            .ToList();
        if (bills.Count == 0)
        {
            return string.Empty;
        }

        var named = string.Join(", ", bills.Take(BillsNamed).Select(b => PaymentText.BillName(BillTitleOf(b), b.Period)));
        return bills.Count > BillsNamed ? _localizer.Get("PaymentPaysMore", named, bills.Count - BillsNamed) : named;
    }

    private string BillTitleOf(Bill bill) =>
        bill.ContractId is { } contractId && _contracts.TryGetValue(contractId, out var contract) ? contract.Title : bill.Title ?? string.Empty;

    private string SymbolOf(Payment payment) => _localizer["Currency_Symbol_" + payment.Currency];

    /// <summary>"Paid 1 234,56 € · Prepayment 12,30 €" per currency (the prepayment is the part no bill uses).</summary>
    private string BuildTotals(List<Payment> rows) =>
        string.Join(
            "\n",
            rows
                .GroupBy(p => p.Currency)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var symbol = _localizer["Currency_Symbol_" + g.Key];
                    return _localizer.Get(
                        "PaymentsTotals",
                        BillText.Money(g.Sum(p => p.Amount), symbol),
                        BillText.Money(g.Sum(p => Math.Max(0m, PaymentRules.Unallocated(p))), symbol));
                }));
}
