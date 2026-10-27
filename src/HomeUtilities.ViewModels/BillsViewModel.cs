using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The Bills screen: the bills filtered by property, period (all years, year or month) and status with their payment badge and the difference to the
/// calculation, the totals of the list, and the "Paid" quick action. Two-pane like Properties; the editor is
/// <see cref="BillEditViewModel"/>.
/// </summary>
public sealed partial class BillsViewModel : MasterDetailViewModel<BillEditViewModel>
{
    protected override HelpTopic GuideTopic => HelpTopic.Bills;

    private readonly IBillStore _billStore;
    private readonly IPaymentStore _paymentStore;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IPropertyStore _propertyStore;
    private readonly QuickPayService _quickPay;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly BillFilterState _filter;

    private IReadOnlyList<Bill> _bills = [];
    private IReadOnlyList<Property> _properties = [];
    private Dictionary<Guid, Contract> _contracts = [];
    private Dictionary<Guid, string> _supplierNames = [];
    private Dictionary<Guid, string> _categoryColors = [];
    private Dictionary<Guid, List<Payment>> _paymentsByBill = [];
    private Guid? _highlightContractId;

    [ObservableProperty]
    private IReadOnlyList<BillItemViewModel> _items = [];

    [ObservableProperty]
    private BillsListTexts _texts;

    [ObservableProperty]
    private string _propertyFilterName = string.Empty;

    [ObservableProperty]
    private string _statusFilterName = string.Empty;

    [ObservableProperty]
    private string _totalsText = string.Empty;

    [ObservableProperty]
    private string _nothingFoundText = string.Empty;

    [ObservableProperty]
    private bool _showNothingFound;

    public BillsViewModel(
        IBillStore billStore,
        IPaymentStore paymentStore,
        IContractStore contractStore,
        ISupplierStore supplierStore,
        ICategoryStore categoryStore,
        IPropertyStore propertyStore,
        QuickPayService quickPay,
        ISnackbarService snackbar,
        IDialogService dialogs,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        ISectionSelectionMemory selectionMemory,
        BillFilterState filter,
        Func<BillEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.BillEdit, selectionMemory)
    {
        _billStore = billStore;
        _paymentStore = paymentStore;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _categoryStore = categoryStore;
        _propertyStore = propertyStore;
        _quickPay = quickPay;
        _snackbar = snackbar;
        _dialogs = dialogs;
        _clock = clock;
        _localizer = localizer;
        _filter = filter;
        _texts = new BillsListTexts(localizer);
        Period = new PeriodFilterViewModel(() => _filter.Period, p => _filter.Period = p, clock, localizer, RebuildItems);
        RefreshTexts();
    }

    /// <summary>The period filter (all years / year / month); the current year until the user changes it.</summary>
    public PeriodFilterViewModel Period { get; }

    public BillStatusFilter StatusFilter => _filter.Status;

    public Guid? PropertyFilterId => _filter.PropertyId;

    protected override int TotalCount => _bills.Count;

    public override async Task ReloadAsync()
    {
        _bills = await _billStore.ListAsync();
        _properties = await _propertyStore.ListAsync(true);
        _contracts = (await _contractStore.ListAsync()).ToDictionary(c => c.Id);
        _supplierNames = (await _supplierStore.ListAsync()).ToDictionary(s => s.Id, s => s.Name);
        _categoryColors = (await _categoryStore.ListAsync()).ToDictionary(c => c.Id, c => c.Color);
        _paymentsByBill = [];
        foreach (var payment in await _paymentStore.ListAsync())
        {
            foreach (var billId in payment.Allocations.Where(a => !a.IsDeleted).Select(a => a.BillId).Distinct())
            {
                if (!_paymentsByBill.TryGetValue(billId, out var list))
                {
                    _paymentsByBill[billId] = list = [];
                }

                list.Add(payment);
            }
        }

        if (_filter.PropertyId is { } filtered && _properties.All(p => p.Id != filtered))
        {
            _filter.PropertyId = null;
        }

        IsLoaded = true;
        RebuildItems();
        RaiseItemsChanged();
    }

    /// <summary>Back from the payment editor: the bill open in the side pane shows the payments as they are now.</summary>
    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();
        if (SelectedEditor is { IsNew: false, IsHostedInPage: false } editor)
        {
            await editor.RefreshPaymentCardAsync();
        }
    }

    /// <summary>Navigation parameter carrying a <see cref="BillStatusFilter"/> name when the screen is opened from the Overview.</summary>
    public const string StatusParameter = "status";

    /// <summary>Navigation parameter carrying a calendar year (shown instead of every year) when the screen is opened from the Overview.</summary>
    public const string YearParameter = "year";

    /// <summary>
    /// Opens the screen for a property with the bills of every year (or of one <paramref name="year"/>) and the given status; the
    /// contract's bills are highlighted ("All bills").
    /// </summary>
    public Task OpenForAsync(Guid propertyId, Guid? contractId, BillStatusFilter status = BillStatusFilter.All, int? year = null) => RunSafeAsync(async () =>
    {
        _filter.PropertyId = propertyId;
        Period.Set(year is { } shown ? PeriodFilter.ForYear(shown) : PeriodFilter.ForAllYears(Period.Current.Year));
        _filter.Status = status;
        _highlightContractId = contractId;
        if (IsLoaded)
        {
            await ReloadAsync();
        }
    });

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionBills"];
        EmptyText = _localizer["BillsEmpty"];
        AddText = _localizer["BillAdd"];
        HelpText = _localizer["SectionHelp"];
        SelectHintText = _localizer["BillSelectHint"];
        NothingFoundText = _localizer["BillsNothingFound"];
        Texts = new BillsListTexts(_localizer);
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _bills.Any(b => b.Id == id);

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
        var all = _localizer["BillAllProperties"];
        var picked = await _dialogs.ChooseAsync(_localizer["BillsPropertyFilter"], [all, .. _properties.Select(p => p.Name)]);
        if (picked is null)
        {
            return;
        }

        _filter.PropertyId = picked == all ? null : _properties.FirstOrDefault(p => p.Name == picked)?.Id;
        _highlightContractId = null;
        RebuildItems();
    });

    [RelayCommand]
    private Task ChooseStatusFilterAsync() => RunSafeAsync(async () =>
    {
        var options = Enum.GetValues<BillStatusFilter>().Select(s => (Filter: s, Name: StatusName(s))).ToList();
        var picked = await _dialogs.ChooseAsync(_localizer["BillsStatusFilter"], [.. options.Select(o => o.Name)]);
        if (picked is null || options.FirstOrDefault(o => o.Name == picked) is not { Name: not null } option)
        {
            return;
        }

        _filter.Status = option.Filter;
        RebuildItems();
    });

    private string StatusName(BillStatusFilter status) => _localizer["BillStatus" + status];

    // ---- "Paid" quick action --------------------------------------------------------------------------------------

    /// <summary>Pays the open amount of a bill (dated today), then "Bill marked as paid · Undo".</summary>
    public Task MarkPaidAsync(Guid billId) => RunSafeAsync(async () =>
    {
        var payment = await _quickPay.MarkPaidAsync(billId);
        await ReloadAsync();
        if (payment is null)
        {
            return;
        }

        if (await _snackbar.ShowAsync(_localizer["BillMarkedPaid"], _localizer["BillUndo"]))
        {
            await _quickPay.UndoAsync(payment.Id);
            await ReloadAsync();
        }
    });

    // ---- Rows and totals ------------------------------------------------------------------------------------------

    private void RebuildItems()
    {
        var today = _clock.Today;
        var rows = new List<(Bill Bill, BillState State)>();
        foreach (var bill in _bills)
        {
            if (_filter.PropertyId is { } property && bill.PropertyId != property)
            {
                continue;
            }

            if (!Period.Current.Contains(bill.Period))
            {
                continue;
            }

            var state = BillStatus.Of(bill, _paymentsByBill.GetValueOrDefault(bill.Id) ?? [], today);
            var include = _filter.Status switch
            {
                BillStatusFilter.Open => !state.IsPaid,
                BillStatusFilter.Overdue => state.IsOverdue,
                BillStatusFilter.Paid => state.IsPaid,
                _ => true,
            };
            if (include)
            {
                rows.Add((bill, state));
            }
        }

        Items =
        [
            .. rows
                .OrderByDescending(r => r.Bill.Period)
                .ThenBy(r => TitleOf(r.Bill), StringComparer.CurrentCultureIgnoreCase)
                .Select(r => new BillItemViewModel(
                    r.Bill,
                    TitleOf(r.Bill),
                    r.Bill.SupplierId is { } s ? _supplierNames.GetValueOrDefault(s, string.Empty) : string.Empty,
                    _categoryColors.GetValueOrDefault(r.Bill.CategoryId, "#808080"),
                    r.State,
                    _localizer["Currency_Symbol_" + r.Bill.Currency],
                    _localizer,
                    _highlightContractId is { } highlighted && r.Bill.ContractId == highlighted,
                    id => SelectCommand.Execute(id),
                    id => _ = MarkPaidAsync(id))),
        ];

        PropertyFilterName = _filter.PropertyId is { } id
            ? _properties.FirstOrDefault(p => p.Id == id)?.Name ?? _localizer["BillAllProperties"]
            : _localizer["BillAllProperties"];
        Period.Refresh();
        StatusFilterName = StatusName(_filter.Status);
        TotalsText = BuildTotals(rows);
        ShowNothingFound = IsLoaded && _bills.Count > 0 && Items.Count == 0;
        MarkSelectedItem();
    }

    private string TitleOf(Bill bill) =>
        bill.ContractId is { } contractId && _contracts.TryGetValue(contractId, out var contract)
            ? contract.Title
            : bill.Title ?? string.Empty;

    /// <summary>"Billed 1 234,56 € · Paid 1 000,00 € · Open 234,56 €" per currency.</summary>
    private string BuildTotals(List<(Bill Bill, BillState State)> rows)
    {
        var lines = rows
            .GroupBy(r => r.Bill.Currency)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var symbol = _localizer["Currency_Symbol_" + g.Key];
                return _localizer.Get(
                    "BillTotals",
                    BillText.Money(g.Sum(r => r.Bill.Gross), symbol),
                    BillText.Money(g.Sum(r => r.State.Paid), symbol),
                    BillText.Money(g.Sum(r => r.State.Open), symbol));
            });
        return string.Join("\n", lines);
    }
}
