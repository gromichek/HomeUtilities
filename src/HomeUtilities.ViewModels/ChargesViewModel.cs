using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
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

    private sealed record Calculated(Contract Contract, string SupplierName, string Color, ChargeInput? Input, CalculatedCharge? Charge);

    private readonly IPropertyStore _propertyStore;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IChargeInputLoader _loader;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly ISettingsStore _settings;
    private readonly ILayoutModeService _layoutModeService;
    private readonly IErrorLog _errorLog;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, bool> _expanded = [];

    private IReadOnlyList<Calculated> _calculated = [];
    private Guid? _selectedPropertyId;
    private Guid? _focusContractId;
    private bool _monthChosen;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyIndex), nameof(HasMultipleProperties), nameof(HasProperties), nameof(ShowNoProperties), nameof(ShowNoContracts))]
    private IReadOnlyList<ChoiceOption> _properties = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthText), nameof(PickerDate))]
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
    private string _monthLabel = string.Empty;

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
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ISettingsStore settings,
        ILayoutModeService layoutModeService,
        IErrorLog errorLog)
    {
        _propertyStore = propertyStore;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _categoryStore = categoryStore;
        _loader = loader;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _settings = settings;
        _layoutModeService = layoutModeService;
        _errorLog = errorLog;
        _isWide = layoutModeService.Current != LayoutMode.Compact;
        _month = YearMonth.From(clock.Today).AddMonths(-1);
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

    /// <summary>"October 2026".</summary>
    public string MonthText
    {
        get
        {
            var text = Month.FirstDay.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            return text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
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

    public override Task OnAppearingAsync() => RunSafeAsync(ReloadAsync);

    /// <summary>Re-reads the property list and recalculates; called whenever the screen appears (tariffs and readings may have changed).</summary>
    public async Task ReloadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var properties = await _propertyStore.ListAsync(false);
            Properties = [.. properties.Select(p => new ChoiceOption(p.Id, p.Name, string.Empty))];

            var remembered = Guid.TryParse(_settings.GetString(LastPropertyKey), out var saved) ? saved : (Guid?)null;
            _selectedPropertyId =
                Properties.Any(p => p.Id == _selectedPropertyId) ? _selectedPropertyId
                : Properties.Any(p => p.Id == remembered) ? remembered
                : Properties.FirstOrDefault()?.Id;

            await CalculateAsync();
            IsLoaded = true;
            OnPropertyChanged(nameof(SelectedPropertyIndex));
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
        MonthLabel = _localizer["ChargesMonthLabel"];
        PreviousMonthText = _localizer["ChargesPreviousMonth"];
        NextMonthText = _localizer["ChargesNextMonth"];
        TotalsLabel = _localizer["ChargesTotalsLabel"];
        IncompleteText = _localizer["ChargesIncomplete"];
        NoPropertiesText = _localizer["ReadingsNoProperties"];
        NoContractsText = _localizer["ChargesNoContracts"];
        OnPropertyChanged(nameof(MonthText));
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
    private async Task CalculateAsync()
    {
        if (_selectedPropertyId is not { } propertyId)
        {
            _calculated = [];
            Rebuild();
            return;
        }

        var contracts = await _contractStore.ListByPropertyAsync(propertyId);
        if (!_monthChosen)
        {
            Month = ChargePeriodDefaults.For(contracts, _clock.Today);
        }

        var period = Month;
        var suppliers = (await _supplierStore.ListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var categories = (await _categoryStore.ListAsync()).ToDictionary(c => c.Id, c => c.Color);

        var results = new List<Calculated>();
        var active = contracts
            .Where(c => !c.IsDeleted && c.ValidFrom <= period.LastDay && (c.ValidTo is null || c.ValidTo >= period.FirstDay))
            .OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase);
        foreach (var contract in active)
        {
            var supplier = suppliers.GetValueOrDefault(contract.SupplierId, string.Empty);
            var color = categories.GetValueOrDefault(contract.CategoryId, "#808080");
            try
            {
                var input = await _loader.LoadAsync(contract.Id, period);
                results.Add(input is null
                    ? new Calculated(contract, supplier, color, null, null)
                    : new Calculated(contract, supplier, color, input, ChargeCalculator.Calculate(input)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorLog.Append($"{nameof(ChargesViewModel)}.{nameof(CalculateAsync)} (contract {contract.Id}, {period})", ex);
                results.Add(new Calculated(contract, supplier, color, null, null));
            }
        }

        _calculated = results;
        Rebuild();
    }

    /// <summary>Builds the cards and totals from the last calculation (also after a language change).</summary>
    private void Rebuild()
    {
        var builder = new ChargeCardBuilder(
            _localizer,
            new ChargeNavigation(OpenComponentAsync, OpenPropertyAsync, OpenReadingsAsync, OpenContractAsync),
            _clock.Today);

        var cards = new List<ChargeCardViewModel>();
        foreach (var item in _calculated)
        {
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
                $"{TariffDescriber.FormatMoney(group.Sum(c => c.Charge!.TotalNet))} {symbol}",
                $"{TariffDescriber.FormatMoney(group.Sum(c => c.Charge!.TotalVat))} {symbol}",
                $"{TariffDescriber.FormatMoney(group.Sum(c => c.Charge!.TotalGross))} {symbol}"));
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

    /// <summary>Readings of the property, dated the last day of the month shown (today if that is still ahead).</summary>
    private Task OpenReadingsAsync(Guid propertyId)
    {
        var date = Month.LastDay > _clock.Today ? _clock.Today : Month.LastDay;
        return RunSafeAsync(() => _navigation.GoToAsync(
            Routes.ReadingsFor,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = propertyId,
                [ReadingsViewModel.DateParameter] = date,
            }));
    }
}
