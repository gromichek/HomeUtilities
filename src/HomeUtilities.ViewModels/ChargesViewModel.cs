using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The charges screen: for one property and month, the charge every active contract should cost, line by line, with the
/// readings behind each quantity. Everything is calculated on demand by <see cref="ChargeCalculator"/>; nothing is stored.
/// </summary>
public sealed partial class ChargesViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    /// <summary>Settings key of the last selected property.</summary>
    public const string LastPropertyKey = "charges.last_property";

    /// <summary>Navigation parameter (<c>yyyy-MM</c>) that opens the screen on a given month.</summary>
    public const string MonthParameter = "month";

    /// <summary>The month the picker can reach at the earliest (the range of <see cref="YearMonth"/>).</summary>
    public static readonly YearMonth EarliestMonth = new(1900, 1);

    /// <summary>The month the picker can reach at the latest.</summary>
    public static readonly YearMonth LatestMonth = new(2200, 12);

    /// <summary>One contract's calculation for its service month (<see cref="Service"/>), which differs from the screen month by the billing timing.</summary>
    private sealed record Calculated(Contract Contract, YearMonth Service, string SupplierName, string Color, ChargeInput? Input, CalculatedCharge? Charge);

    private readonly IPropertyStore _propertyStore;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IChargeInputLoader _loader;
    private readonly EqualizedSettlementService _equalized;
    private readonly IBillStore _billStore;
    private readonly IPaymentStore _paymentStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly ISettingsStore _settings;
    private readonly ILayoutModeService _layoutModeService;
    private readonly IErrorLog _errorLog;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ReloadGate? _reloadGate;
    private readonly Dictionary<Guid, bool> _expanded = [];

    private IReadOnlyList<Calculated> _calculated = [];
    private Dictionary<Guid, (Bill Bill, BillState State)> _billOfContract = [];
    private Dictionary<Guid, EqualizedReport> _equalizedOfContract = [];
    private Guid? _selectedPropertyId;
    private Guid? _focusContractId;
    private bool _monthChosen;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyIndex), nameof(HasMultipleProperties), nameof(HasProperties), nameof(ShowNoProperties), nameof(ShowNoContracts))]
    private IReadOnlyList<ChoiceOption> _properties = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthText), nameof(PickerDate), nameof(MonthLabel))]
    [NotifyCanExecuteChangedFor(nameof(PreviousMonthCommand), nameof(NextMonthCommand))]
    private YearMonth _month;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCards), nameof(ShowNoContracts))]
    private IReadOnlyList<ChargeCardViewModel> _cards = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoProperties), nameof(ShowNoContracts))]
    private bool _isLoaded;

    [ObservableProperty]
    private string _totalsText = string.Empty;

    [ObservableProperty]
    private bool _isIncomplete;

    [ObservableProperty]
    private ChargeCardViewModel? _focusedCard;

    [ObservableProperty]
    private bool _isWide;

    [ObservableProperty]
    private string _propertyLabel = string.Empty;

    [ObservableProperty]
    private string _previousMonthText = string.Empty;

    [ObservableProperty]
    private string _nextMonthText = string.Empty;

    [ObservableProperty]
    private string _totalsLabel = string.Empty;

    [ObservableProperty]
    private string _incompleteText = string.Empty;

    [ObservableProperty]
    private string _noPropertiesText = string.Empty;

    [ObservableProperty]
    private string _noContractsText = string.Empty;

    public ChargesViewModel(
        IPropertyStore propertyStore,
        IContractStore contractStore,
        ISupplierStore supplierStore,
        ICategoryStore categoryStore,
        IChargeInputLoader loader,
        EqualizedSettlementService equalized,
        IBillStore billStore,
        IPaymentStore paymentStore,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ISettingsStore settings,
        ILayoutModeService layoutModeService,
        IErrorLog errorLog,
        IDataChangeNotifier? changes = null)
    {
        _reloadGate = changes is null ? null : new ReloadGate(changes, clock);
        _propertyStore = propertyStore;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _categoryStore = categoryStore;
        _loader = loader;
        _equalized = equalized;
        _billStore = billStore;
        _paymentStore = paymentStore;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _settings = settings;
        _layoutModeService = layoutModeService;
        _errorLog = errorLog;
        _isWide = layoutModeService.Current != LayoutMode.Compact;
        _month = YearMonth.From(clock.Today);
        RefreshTexts();
    }

    /// <summary>The last property change started from the picker.</summary>
    public Task PendingPropertyChange { get; private set; } = Task.CompletedTask;

    public bool HasProperties => Properties.Count > 0;

    public bool HasMultipleProperties => Properties.Count > 1;

    public bool HasCards => Cards.Count > 0;

    public bool ShowNoProperties => IsLoaded && Properties.Count == 0;

    public bool ShowNoContracts => IsLoaded && Properties.Count > 0 && Cards.Count == 0;

    public Guid? SelectedPropertyId => _selectedPropertyId;

    /// <summary>"November 2026 · to pay": the screen month is the month of payment (T-054).</summary>
    public string MonthLabel => _localizer.Get("ChargesMonthHeader", MonthFormat.Long(Month));

    /// <summary>"October 2026".</summary>
    public string MonthText
    {
        get
        {
            return MonthFormat.Long(Month);
        }
    }

    /// <summary>
    /// The month as a date for the platform date picker (only year and month count). Dates outside the supported
    /// range are clamped, so a picker cannot push the screen out of range.
    /// </summary>
    public DateOnly PickerDate
    {
        get => Month.FirstDay;
        set
        {
            var month = value.Year < EarliestMonth.Year ? EarliestMonth
                : value.Year > LatestMonth.Year ? LatestMonth
                : YearMonth.From(value);
            PendingMonthChange = ChooseMonthAsync(month);
        }
    }

    /// <summary>The last month change started from a button or the picker.</summary>
    public Task PendingMonthChange { get; private set; } = Task.CompletedTask;

    public int SelectedPropertyIndex
    {
        get => Properties.ToList().FindIndex(p => p.Id == _selectedPropertyId);
        set
        {
            if (value < 0 || value >= Properties.Count || Properties[value].Id == _selectedPropertyId)
            {
                return;
            }

            PendingPropertyChange = ChangePropertyAsync(Properties[value].Id);
        }
    }

    /// <summary>
    /// Opens the screen on a property (and a contract to scroll to) with the given month, else the default one; used by
    /// "Show charges".
    /// </summary>
    public Task OpenForAsync(Guid propertyId, Guid? contractId, YearMonth? month = null) => RunSafeAsync(async () =>
    {
        _selectedPropertyId = propertyId;
        _focusContractId = contractId;
        _monthChosen = month is not null;
        if (month is { } chosen)
        {
            Month = chosen;
        }

        _settings.SetString(LastPropertyKey, propertyId.ToString());
        if (IsLoaded)
        {
            await ReloadAsync();
        }
    });

    protected override bool LoadingHintApplies => !IsLoaded;

    /// <summary>"Loading…" shown in place of the content while the first load takes longer than 300 ms (R-52).</summary>
    public string LoadingText => _localizer["Loading"];

    public override Task OnAppearingAsync() => RunSafeAsync(async () =>
    {
        if (_reloadGate is { IsFresh: true } && IsLoaded)
        {
            // Nothing was saved since the last calculation, same day, same language: keep the cards (T-028h).
            return;
        }

        await RunLoadAsync(ct => ReloadAsync(ct), () => Cards.Count);
    });

    /// <summary>Re-reads the property list and recalculates; called whenever the screen appears (tariffs and readings may have changed).</summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var snapshot = _reloadGate?.Snapshot();
            var properties = await OffUiAsync(() => _propertyStore.ListAsync(false, ct), ct);
            ct.ThrowIfCancellationRequested();
            Properties = [.. properties.Select(p => new ChoiceOption(p.Id, p.Name, string.Empty))];

            var remembered = Guid.TryParse(_settings.GetString(LastPropertyKey), out var saved) ? saved : (Guid?)null;
            _selectedPropertyId =
                Properties.Any(p => p.Id == _selectedPropertyId) ? _selectedPropertyId
                : Properties.Any(p => p.Id == remembered) ? remembered
                : Properties.FirstOrDefault()?.Id;

            await CalculateAsync(ct);
            IsLoaded = true;
            OnPropertyChanged(nameof(SelectedPropertyIndex));
            if (snapshot is { } taken)
            {
                _reloadGate?.MarkLoaded(taken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionCharges"];
        HelpText = _localizer["SectionHelp"];
        PropertyLabel = _localizer["ReadingsPropertyLabel"];
        PreviousMonthText = _localizer["ChargesPreviousMonth"];
        NextMonthText = _localizer["ChargesNextMonth"];
        TotalsLabel = _localizer["ChargesTotalsLabel"];
        IncompleteText = _localizer["ChargesIncomplete"];
        NoPropertiesText = _localizer["ReadingsNoProperties"];
        NoContractsText = _localizer["ChargesNoContracts"];
        OnPropertyChanged(nameof(MonthText));
        OnPropertyChanged(nameof(MonthLabel));
        _reloadGate?.Invalidate();
        if (IsLoaded)
        {
            Rebuild();
        }
    }

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message)
    {
        IsWide = _layoutModeService.Current != LayoutMode.Compact;
        foreach (var card in Cards)
        {
            card.IsExpanded = IsWide;
        }
    }

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, HelpTopic.Charges));

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousMonthAsync() => CanGoPrevious() ? ChooseMonthAsync(Month.AddMonths(-1)) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextMonthAsync() => CanGoNext() ? ChooseMonthAsync(Month.AddMonths(1)) : Task.CompletedTask;

    private bool CanGoPrevious() => Month > EarliestMonth;

    private bool CanGoNext() => Month < LatestMonth;

    /// <summary>Switches to another property; the month goes back to that property's default.</summary>
    public Task ChangePropertyAsync(Guid propertyId) => RunSafeAsync(async () =>
    {
        if (propertyId == _selectedPropertyId)
        {
            return;
        }

        _selectedPropertyId = propertyId;
        _focusContractId = null;
        _monthChosen = false;
        _expanded.Clear();
        _settings.SetString(LastPropertyKey, propertyId.ToString());
        await _gate.WaitAsync();
        try
        {
            await CalculateAsync();
        }
        finally
        {
            _gate.Release();
        }

        OnPropertyChanged(nameof(SelectedPropertyIndex));
    });

    private Task ChooseMonthAsync(YearMonth month) => RunSafeAsync(async () =>
    {
        if (month == Month)
        {
            return;
        }

        _monthChosen = true;
        _focusContractId = null;
        await _gate.WaitAsync();
        try
        {
            Month = month;
            await CalculateAsync();
        }
        finally
        {
            _gate.Release();
        }
    });

    /// <summary>Loads the contracts of the selected property and calculates each one; a failing contract becomes an error card.</summary>
    private async Task CalculateAsync(CancellationToken ct = default)
    {
        if (_selectedPropertyId is not { } propertyId)
        {
            _calculated = [];
            _billOfContract = [];
            _equalizedOfContract = [];
            Rebuild();
            return;
        }

        var monthChosen = _monthChosen;
        var chosenMonth = Month;
        var today = _clock.Today;

        // Database reads and the calculation of every contract run on a pool thread; only the final assignment is on the UI thread.
        var data = await OffUiAsync(async () => await CalculateCoreAsync(propertyId, monthChosen, chosenMonth, today, ct), ct);
        ct.ThrowIfCancellationRequested();
        if (!_monthChosen)
        {
            Month = data.Period;
        }

        _calculated = data.Results;
        _billOfContract = data.Bills;
        _equalizedOfContract = data.Equalized;
        Rebuild();
    }

    private async Task<(YearMonth Period, List<Calculated> Results, Dictionary<Guid, (Bill Bill, BillState State)> Bills, Dictionary<Guid, EqualizedReport> Equalized)>
        CalculateCoreAsync(Guid propertyId, bool monthChosen, YearMonth chosenMonth, DateOnly today, CancellationToken ct)
    {
        var contracts = await _contractStore.ListByPropertyAsync(propertyId, ct);
        // The screen month is the month of payment; the default is the current month (T-054).
        var period = monthChosen ? chosenMonth : YearMonth.From(today);
        var suppliers = (await _supplierStore.ListAsync(ct)).ToDictionary(s => s.Id, s => s.Name);
        var categories = (await _categoryStore.ListAsync(ct)).ToDictionary(c => c.Id, c => c.Color);

        var results = new List<Calculated>();
        var activeList = contracts
            .Select(c => (Contract: c, Service: ServiceMonth(c, period)))
            .Where(x => !x.Contract.IsDeleted && x.Contract.ValidFrom <= x.Service.LastDay && (x.Contract.ValidTo is null || x.Contract.ValidTo >= x.Service.FirstDay))
            .OrderBy(x => x.Contract.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // One read for all contracts of the property (T-028h); if it fails, each contract is loaded alone so only the broken one gets an error card.
        IReadOnlyDictionary<(Guid ContractId, YearMonth Period), ChargeInput>? batch = null;
        try
        {
            batch = await _loader.LoadManyAsync([.. activeList.Select(x => (x.Contract.Id, x.Service))], ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not logged here: the per-contract loads below hit the same error and log it for the contract concerned.
            batch = null;
        }

        foreach (var (contract, service) in activeList)
        {
            var supplier = suppliers.GetValueOrDefault(contract.SupplierId, string.Empty);
            var color = categories.GetValueOrDefault(contract.CategoryId, "#808080");
            try
            {
                var input = batch is null ? await _loader.LoadAsync(contract.Id, service, ct) : batch.GetValueOrDefault((contract.Id, service));
                results.Add(input is null
                    ? new Calculated(contract, service, supplier, color, null, null)
                    : new Calculated(contract, service, supplier, color, input, ChargeCalculator.Calculate(input)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorLog.Append($"{nameof(ChargesViewModel)}.{nameof(CalculateAsync)} (contract {contract.Id}, {service})", ex);
                results.Add(new Calculated(contract, service, supplier, color, null, null));
            }
        }

        var bills = await LoadBillsAsync(propertyId, activeList, today, ct);
        var equalized = await LoadEqualizedAsync(activeList.Select(x => x.Contract), ct);
        return (period, results, bills, equalized);
    }

    /// <summary>
    /// The service month a contract is calculated for when the screen shows payment month <paramref name="payment"/>:
    /// the payment month minus the arrival offset of its billing timing (after the period 1, in the month 0, in advance −1).
    /// A changed timing applies to past screen months too (documented limitation).
    /// </summary>
    internal static YearMonth ServiceMonth(Contract contract, YearMonth payment) =>
        payment.AddMonths(-contract.BillingTiming.ArrivalOffset());

    /// <summary>The settlement report of each equalized contract; a failing one is logged and left out.</summary>
    private async Task<Dictionary<Guid, EqualizedReport>> LoadEqualizedAsync(IEnumerable<Contract> contracts, CancellationToken ct)
    {
        var result = new Dictionary<Guid, EqualizedReport>();
        foreach (var contract in contracts.Where(c => c.BillingMode == BillingMode.Equalized))
        {
            try
            {
                result[contract.Id] = await _equalized.LoadAsync(contract.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorLog.Append($"{nameof(ChargesViewModel)}.{nameof(LoadEqualizedAsync)} (contract {contract.Id})", ex);
            }
        }

        return result;
    }

    /// <summary>The bill of each contract for the month (the first one entered) with its payment state.</summary>
    private async Task<Dictionary<Guid, (Bill Bill, BillState State)>> LoadBillsAsync(
        Guid propertyId, IReadOnlyList<(Contract Contract, YearMonth Service)> contracts, DateOnly today, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (Bill, BillState)>();
        if (contracts.Count == 0)
        {
            return result;
        }

        var serviceOf = contracts.ToDictionary(x => x.Contract.Id, x => x.Service);
        var bills = (await _billStore.ListByPropertyAsync(propertyId, serviceOf.Values.Min(), serviceOf.Values.Max(), ct))
            .Where(b => b.ContractId is { } id && serviceOf.TryGetValue(id, out var service) && b.Period == service)
            .OrderBy(b => b.CreatedAt);
        foreach (var bill in bills)
        {
            if (result.ContainsKey(bill.ContractId!.Value))
            {
                continue;
            }

            var payments = await _paymentStore.ListByBillAsync(bill.Id, ct);
            result[bill.ContractId.Value] = (bill, BillStatus.Of(bill, payments, today));
        }

        return result;
    }

    /// <summary>Builds the cards and totals from the last calculation (also after a language change).</summary>
    private void Rebuild()
    {
        var cards = new List<ChargeCardViewModel>();
        foreach (var item in _calculated)
        {
            // Everything on a card works on the card's own service month, never on the screen month (T-054).
            var service = item.Service;
            var builder = new ChargeCardBuilder(
                _localizer,
                new ChargeNavigation(OpenComponentAsync, OpenPropertyAsync, propertyId => OpenReadingsAsync(propertyId, service), OpenContractAsync),
                _clock.Today);
            ChargeCardViewModel card;
            try
            {
                card = item.Input is null || item.Charge is null
                    ? builder.BuildError(item.Contract, item.SupplierName, item.Color)
                    : builder.Build(item.Input, item.Charge, item.SupplierName, item.Color);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorLog.Append($"{nameof(ChargesViewModel)}.{nameof(Rebuild)} (contract {item.Contract.Id})", ex);
                card = builder.BuildError(item.Contract, item.SupplierName, item.Color);
            }

            var id = item.Contract.Id;
            card.ServiceMonthText = _localizer.Get("ChargeServiceMonth", MonthFormat.Long(service));
            ApplyBill(card, item.Contract, item.Charge, service);
            ApplyEqualized(card, item.Contract, item.Charge, service);
            card.IsExpanded = _focusContractId == id || _expanded.GetValueOrDefault(id, IsWide);
            card.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ChargeCardViewModel.IsExpanded))
                {
                    _expanded[id] = card.IsExpanded;
                }
            };
            cards.Add(card);
        }

        Cards = cards;
        FocusedCard = _focusContractId is { } focus ? cards.FirstOrDefault(c => c.ContractId == focus) : null;
        BuildTotals(cards);
    }

    /// <summary>The plan, the actual charge and the running difference since the last settlement of an equalized contract.</summary>
    private void ApplyEqualized(ChargeCardViewModel card, Contract contract, CalculatedCharge? charge, YearMonth service)
    {
        if (!_equalizedOfContract.TryGetValue(contract.Id, out var report) || report.Plans.Count == 0)
        {
            return;
        }

        var symbol = _localizer["Currency_Symbol_" + contract.Currency];
        var plan = EqualizedPlans.AmountFor(report.Plans, service);
        if (plan is null)
        {
            // A month before the first plan was billed by consumption: a normal card.
            return;
        }

        card.EqualizedText = _localizer.Get(
            "ChargesEqualizedLine",
            BillText.Money(plan.Value, symbol),
            charge is { IsComplete: true } ? BillText.Money(charge.TotalGross, symbol) : "–",
            BillText.SignedMoney(report.Settlement.Expected, symbol));
    }

    /// <summary>The card's link to the month's bill, or the "Enter bill" button with the calculated total as a suggestion.</summary>
    private void ApplyBill(ChargeCardViewModel card, Contract contract, CalculatedCharge? charge, YearMonth service)
    {
        if (_billOfContract.TryGetValue(contract.Id, out var found))
        {
            var (bill, state) = found;
            var symbol = _localizer["Currency_Symbol_" + bill.Currency];
            var parts = new List<string> { BillText.Money(bill.Gross, symbol) };
            var comparison = BillComparison.Compare(bill, bill.CalculatedSnapshot, null);
            if (comparison.Difference is { } difference)
            {
                parts.Add(BillText.SignedMoney(difference, symbol));
            }

            parts.Add(BillBadge.TextOf(_localizer, state, symbol));
            card.BillText = _localizer.Get("ChargesBillLine", string.Join(" · ", parts));
            card.OpenBillCommand = new RelayCommand(() => _ = RunSafeAsync(() => _navigation.GoToAsync(
                Routes.BillEdit,
                new Dictionary<string, object> { [EditorNavigation.IdParameter] = bill.Id })));
            return;
        }

        card.EnterBillText = _localizer["ChargesEnterBill"];
        card.EnterBillCommand = new RelayCommand(() => _ = RunSafeAsync(() => EnterBillAsync(contract, charge, service)));
    }

    private Task EnterBillAsync(Contract contract, CalculatedCharge? charge, YearMonth service)
    {
        var parameters = new Dictionary<string, object>
        {
            [EditorNavigation.ContractIdParameter] = contract.Id,
            [EditorNavigation.PropertyIdParameter] = contract.PropertyId,
            [BillEditViewModel.PeriodParameter] = service.ToString(),
        };
        if (charge is { IsComplete: true, Lines.Count: > 0 } && charge.Lines.Any(l => !l.IsFromBill))
        {
            parameters[BillEditViewModel.GrossParameter] = charge.TotalGross;
        }

        return _navigation.GoToAsync(Routes.BillEdit, parameters);
    }

    private void BuildTotals(IReadOnlyList<ChargeCardViewModel> cards)
    {
        var lines = new List<string>();
        var groups = _calculated
            .Where(c => c.Charge is not null && c.Input is not null)
            .Where(c => cards.FirstOrDefault(card => card.ContractId == c.Contract.Id) is { Status: ChargeCardStatus.Complete or ChargeCardStatus.Incomplete })
            .GroupBy(c => c.Charge!.Currency);
        foreach (var group in groups)
        {
            var symbol = _localizer["Currency_Symbol_" + group.Key];
            lines.Add(_localizer.Get(
                "ChargesTotals",
                BillText.Money(group.Sum(c => c.Charge!.TotalNet), symbol),
                BillText.Money(group.Sum(c => c.Charge!.TotalVat), symbol),
                BillText.Money(group.Sum(c => c.Charge!.TotalGross), symbol)));
        }

        TotalsText = string.Join("\n", lines);
        IsIncomplete = cards.Any(c => c.Status is ChargeCardStatus.Incomplete or ChargeCardStatus.Error);
    }

    private Task OpenComponentAsync(Guid contractId, Guid componentId)
    {
        var parameters = new Dictionary<string, object> { [EditorNavigation.ContractIdParameter] = contractId };
        if (componentId != Guid.Empty)
        {
            parameters[EditorNavigation.IdParameter] = componentId;
        }

        return RunSafeAsync(() => _navigation.GoToAsync(Routes.TariffComponentEdit, parameters));
    }

    private Task OpenPropertyAsync(Guid propertyId) =>
        RunSafeAsync(() => _navigation.GoToAsync(Routes.PropertyEdit, new Dictionary<string, object> { [EditorNavigation.IdParameter] = propertyId }));

    private Task OpenContractAsync(Guid propertyId, Guid contractId) =>
        RunSafeAsync(() => _navigation.GoToAsync(
            Routes.ContractEdit,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = propertyId,
                [EditorNavigation.IdParameter] = contractId,
            }));

    /// <summary>Readings of the property, dated the last day of the card's service month (today if that is still ahead).</summary>
    private Task OpenReadingsAsync(Guid propertyId, YearMonth service)
    {
        var date = service.LastDay > _clock.Today ? _clock.Today : service.LastDay;
        return RunSafeAsync(() => _navigation.GoToAsync(
            Routes.ReadingsFor,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = propertyId,
                [ReadingsViewModel.DateParameter] = date,
            }));
    }
}
