using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Editor of one tariff component of a contract (what is counted, VAT, prices over time, a live example). It is a page in
/// every layout; see <see cref="Routes.TariffComponentEdit"/>. Set <see cref="ContractId"/> before <see cref="LoadAsync"/>.
/// </summary>
public sealed partial class TariffComponentEditViewModel : PaneEditorViewModel
{
    protected override HelpTopic? GuideTopic => HelpTopic.Tariffs;

    private static readonly Regex RateErrorField = new(@"^Rates\[(\d+)\]\.", RegexOptions.Compiled);

    private static readonly PropertyParameterKind[] AreaParameters =
        [PropertyParameterKind.TotalArea, PropertyParameterKind.HeatedArea, PropertyParameterKind.LandArea, PropertyParameterKind.Custom];

    private static readonly PropertyParameterKind[] ResidentParameters =
        [PropertyParameterKind.DeclaredResidents, PropertyParameterKind.Custom];

    /// <summary>The properties that mark the editor dirty when the user changes them.</summary>
    private static readonly HashSet<string> DirtyFields =
    [
        nameof(ComponentName), nameof(SelectedService), nameof(SelectedCategory), nameof(SelectedBasis), nameof(Notes),
        nameof(SelectedVatOption), nameof(VatPercentText), nameof(SelectedParameter), nameof(CustomParameterName),
    ];

    /// <summary>The properties whose change can change the worked example or the "now" line.</summary>
    private static readonly HashSet<string> ExampleFields =
    [
        nameof(SelectedBasis), nameof(SelectedVatOption), nameof(VatPercentText), nameof(SelectedParameter), nameof(CustomParameterName),
    ];

    private readonly ITariffStore _store;
    private readonly IContractStore _contractStore;
    private readonly IMeterStore _meterStore;
    private readonly IPropertyStore _propertyStore;
    private readonly IVatRateStore _vatStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly List<TariffRateRowViewModel> _rateRows = [];
    private readonly List<TariffMeterSwitchViewModel> _meterSwitches = [];
    private readonly List<TariffRegisterSwitchViewModel> _registerSwitches = [];

    private TariffComponent _component = new();
    private TariffComponent? _pending;
    private List<TariffRateRowViewModel> _buildOrder = [];
    private Contract _contract = new();
    private Property? _property;
    private IReadOnlyList<Meter> _linkedMeters = [];
    private IReadOnlyList<VatRate> _catalog = [];
    private IReadOnlyList<Category> _categories = [];
    private IReadOnlyList<ValidationError> _inputErrors = [];

    [ObservableProperty]
    private string _componentName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedServiceIndex))]
    private IReadOnlyList<EnumOption> _services = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedServiceIndex))]
    private EnumOption? _selectedService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryName), nameof(SelectedCategoryColor), nameof(HasCategoryColor))]
    private ChoiceOption? _selectedCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBasisIndex))]
    private IReadOnlyList<EnumOption> _bases = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(SelectedBasisIndex), nameof(BasisHint), nameof(ShowMeterSelection), nameof(ShowParameter), nameof(ShowCountedCard),
        nameof(ShowPrices), nameof(ShowRegisterSelection))]
    private EnumOption? _selectedBasis;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMeters))]
    private IReadOnlyList<TariffMeterSwitchViewModel> _meters = [];

    [ObservableProperty]
    private IReadOnlyList<TariffRegisterSwitchViewModel> _registers = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedParameterIndex))]
    private IReadOnlyList<EnumOption> _parameters = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedParameterIndex), nameof(ShowCustomParameter))]
    private EnumOption? _selectedParameter;

    [ObservableProperty]
    private string _customParameterName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedVatIndex))]
    private IReadOnlyList<VatOption> _vatOptions = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedVatIndex), nameof(IsCustomVat))]
    private VatOption? _selectedVatOption;

    [ObservableProperty]
    private string _vatPercentText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRates))]
    private IReadOnlyList<TariffRateRowViewModel> _rates = [];

    public TariffComponentEditViewModel(
        ITariffStore store,
        IContractStore contractStore,
        IMeterStore meterStore,
        IPropertyStore propertyStore,
        IVatRateStore vatStore,
        ICategoryStore categoryStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar)
        : base(localizer, dialogs, navigation, snackbar)
    {
        _store = store;
        _contractStore = contractStore;
        _meterStore = meterStore;
        _propertyStore = propertyStore;
        _vatStore = vatStore;
        _categoryStore = categoryStore;
        _clock = clock;
        _localizer = localizer;
        RefreshTexts();
    }

    public override Guid EntityId => _component.Id;

    /// <summary>Id of the contract the component belongs to; set before <see cref="LoadAsync"/> for a new component.</summary>
    public Guid ContractId { get; set; }

    public string SelectedCategoryName => SelectedCategory?.Name ?? _localizer["TariffCategorySameAsContract"];

    public string SelectedCategoryColor => SelectedCategory?.Color ?? string.Empty;

    public bool HasCategoryColor => SelectedCategoryColor.Length > 0;

    public ChargeBasis Basis => SelectedBasis?.As<ChargeBasis>() ?? ChargeBasis.PerUnit;

    /// <summary>Meters and registers are chosen for consumption-based bases.</summary>
    public bool ShowMeterSelection => Basis is ChargeBasis.PerUnit or ChargeBasis.NetExport;

    public bool ShowRegisterSelection => ShowMeterSelection;

    /// <summary>The parameter of the property is chosen for area and resident bases.</summary>
    public bool ShowParameter => Basis is ChargeBasis.PerArea or ChargeBasis.PerResident;

    /// <summary>The "What is counted" card is shown for every basis that counts something.</summary>
    public bool ShowCountedCard => ShowMeterSelection || ShowParameter;

    /// <summary>A bill-based component has no price of its own.</summary>
    public bool ShowPrices => Basis != ChargeBasis.FromBill;

    public bool HasNoMeters => Meters.Count == 0;

    public bool HasNoRates => Rates.Count == 0;

    public bool ShowCustomParameter => SelectedParameter?.Value is PropertyParameterKind.Custom;

    public bool IsCustomVat => SelectedVatOption?.IsCustom == true;

    public int SelectedServiceIndex
    {
        get => IndexOf(Services, SelectedService);
        set => SelectedService = Pick(Services, value) ?? SelectedService;
    }

    public int SelectedBasisIndex
    {
        get => IndexOf(Bases, SelectedBasis);
        set => SelectedBasis = Pick(Bases, value) ?? SelectedBasis;
    }

    public int SelectedParameterIndex
    {
        get => IndexOf(Parameters, SelectedParameter);
        set => SelectedParameter = Pick(Parameters, value) ?? SelectedParameter;
    }

    public int SelectedVatIndex
    {
        get => SelectedVatOption is null ? -1 : VatOptions.ToList().IndexOf(SelectedVatOption);
        set
        {
            if (value >= 0 && value < VatOptions.Count)
            {
                SelectedVatOption = VatOptions[value];
            }
        }
    }

    /// <summary>One-line explanation of the selected basis.</summary>
    public string BasisHint => _localizer["TariffBasisHint_" + Basis];

    /// <summary>"now: 52,3 m²" for the chosen parameter of the property.</summary>
    public string ParameterNowText => Describer().ParameterNowText(BuildComponent(record: false));

    /// <summary>The worked example with the newest price, live.</summary>
    public TariffExample Example => Describer().Example(BuildComponent(record: false));

    public string ExampleText => Example.Text;

    public string NameError => Lookup(nameof(TariffComponent.Name));

    public string NotesError => Lookup(nameof(TariffComponent.Notes));

    public string VatError => Join(Lookup(nameof(TariffComponent.VatCode)), Lookup(nameof(TariffComponent.VatPercentOverride)));

    public string CountedError => Join(
        Lookup(nameof(TariffComponent.RegisterKinds)),
        Lookup(nameof(TariffComponent.MeterIds)),
        Lookup(nameof(TariffComponent.QuantityParameter)),
        Lookup(nameof(TariffComponent.QuantityParameterName)));

    public string RatesError => Lookup(nameof(TariffComponent.Rates));

    // Texts. Get-only so that adding one is a single line; RefreshTexts raises them all after a language change.
    public string GeneralHeader => _localizer["TariffGeneral"];

    public string NameLabel => _localizer["TariffNameLabel"];

    public string ServiceLabel => _localizer["TariffServiceLabel"];

    public string CategoryLabel => _localizer["TariffCategoryLabel"];

    public string BasisLabel => _localizer["TariffBasisLabel"];

    public string NotesHeader => _localizer["TariffNotes"];

    public string CountedHeader => _localizer["TariffCountedHeader"];

    public string MetersLabel => _localizer["TariffMetersLabel"];

    public string NoMetersText => _localizer["TariffNoMeters"];

    public string RegistersLabel => _localizer["TariffRegistersLabel"];

    public string ParameterLabel => _localizer["TariffParameterLabel"];

    public string CustomParameterLabel => _localizer["TariffCustomParameterLabel"];

    public string VatHeader => _localizer["TariffVatHeader"];

    public string VatPercentLabel => _localizer["TariffVatPercentLabel"];

    public string PricesHeader => _localizer["TariffPricesHeader"];

    public string PricesEmptyText => _localizer["TariffPricesEmpty"];

    public string AddRateText => _localizer["TariffRateAdd"];

    public string ExampleHeader => _localizer["TariffExampleHeader"];

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var existing = id is { } value ? await _store.GetAsync(value) : null;
            _component = existing ?? new TariffComponent { ContractId = ContractId };
            ContractId = _component.ContractId;
            IsNew = existing is null;

            _contract = await _contractStore.GetAsync(ContractId) ?? new Contract { Id = ContractId };
            _property = await _propertyStore.GetAsync(_contract.PropertyId);
            _catalog = await _vatStore.ListAsync();
            _categories = await _categoryStore.ListAsync();
            await LoadMetersAsync();

            Services = EnumText.Options<ServiceKind>(_localizer);
            Bases = EnumText.Options<ChargeBasis>(_localizer);
            SelectedService = Services.First(o => o.Value.Equals(IsNew ? _contract.Service : _component.ChargeKind));
            SelectedBasis = Bases.First(o => o.Value.Equals(_component.Basis));
            SelectedCategory = _component.CategoryId is { } categoryId ? FindCategoryOption(categoryId) : null;
            ComponentName = _component.Name;
            Notes = _component.Notes ?? string.Empty;

            _rateRows.Clear();
            foreach (var rate in _component.Rates.Where(r => !r.IsDeleted))
            {
                _rateRows.Add(CreateRateRow(rate));
            }

            BuildMeterSwitches();
            _registerSwitches.Clear();
            RebuildRegisters();
            RebuildParameters();
            RebuildVatOptions();
            RebuildRates();

            ClearErrors();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }

        RaiseExample();
    }

    public override async Task OnAppearingAsync()
    {
        // Nothing else edits a component while its page is open, but the property or the VAT catalog may have changed
        // behind a child page (e.g. help); a cheap re-read keeps the example honest.
        _catalog = await _vatStore.ListAsync();
        RaiseExample();
    }

    /// <summary>Opens the category list; the first entry returns to "same as contract".</summary>
    [RelayCommand]
    private Task ChooseCategoryAsync() => RunSafeAsync(async () =>
    {
        var same = _localizer["TariffCategorySameAsContract"];
        var options = CategoryOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["TariffChooseCategory"], [same, .. options.Select(o => o.Name)]);
        if (picked is null)
        {
            return;
        }

        SelectedCategory = picked == same ? null : options.FirstOrDefault(o => o.Name == picked) ?? SelectedCategory;
    });

    [RelayCommand]
    private Task AddRateAsync() => RunSafeAsync(async () =>
    {
        var newest = _rateRows.OrderByDescending(r => r.Rate.ValidFrom).FirstOrDefault()?.Rate;
        var editor = new TariffRateEditorViewModel(
            _localizer, _clock, null, CurrencyOfContract(), Basis != ChargeBasis.NetExport, newest, candidate => IsDuplicateRate(candidate, null));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        result.ComponentId = _component.Id;
        _rateRows.Add(CreateRateRow(result));
        RebuildRates();
        MarkDirty();
    });

    [RelayCommand]
    private Task EditRateAsync(TariffRateRowViewModel row) => RunSafeAsync(async () =>
    {
        var editor = new TariffRateEditorViewModel(
            _localizer, _clock, row.Rate, CurrencyOfContract(), Basis != ChargeBasis.NetExport, null, candidate => IsDuplicateRate(candidate, row));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        result.ComponentId = _component.Id;
        row.Rate = result;
        row.ErrorText = string.Empty;
        RebuildRates();
        MarkDirty();
    });

    [RelayCommand]
    private Task RemoveRateAsync(TariffRateRowViewModel row) => RunSafeAsync(async () =>
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", row.DisplayText),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _rateRows.Remove(row);
        RebuildRates();
        MarkDirty();
    });

    protected override IReadOnlyList<ValidationError> Validate()
    {
        _inputErrors = [];
        _pending = BuildComponent(record: true);
        var errors = new List<ValidationError>(_inputErrors);
        errors.AddRange(TariffComponentValidator.Validate(_pending).Errors);
        return errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildComponent(record: false);
        if (IsNew)
        {
            var all = await _store.ListByContractAsync(ContractId);
            pending.SortOrder = all.Select(c => c.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        }
        else
        {
            pending.SortOrder = _component.SortOrder;
        }

        await _store.SaveAsync(pending);
        _component = pending;
        IsNew = false;
        Title = TitleText();
    }

    protected override Task DeleteCoreAsync() => _store.DeleteAsync(_component.Id);

    protected override string DeleteName() => _component.Name;

    protected override void OnErrorsChanged()
    {
        foreach (var name in new[] { nameof(NameError), nameof(NotesError), nameof(VatError), nameof(CountedError), nameof(RatesError) })
        {
            OnPropertyChanged(name);
        }

        foreach (var row in _rateRows)
        {
            row.ErrorText = string.Empty;
        }

        foreach (var (field, message) in Errors)
        {
            var match = RateErrorField.Match(field);
            if (match.Success
                && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var index
                && index < _buildOrder.Count)
            {
                var row = _buildOrder[index];
                row.ErrorText = row.ErrorText.Length == 0 ? message : row.ErrorText + "\n" + message;
            }
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not { } name)
        {
            return;
        }

        if (DirtyFields.Contains(name))
        {
            MarkDirty();
        }

        if (ExampleFields.Contains(name))
        {
            RaiseExample();
        }
    }

    partial void OnSelectedBasisChanged(EnumOption? value)
    {
        if (SuppressDirty)
        {
            return;
        }

        RebuildRegisters();
        RebuildParameters();
        RebuildRates();
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var service = SelectedService?.Value;
            var basis = SelectedBasis?.Value;
            var parameter = SelectedParameter?.Value;
            var vat = SelectedVatOption?.Code;
            var vatCustom = SelectedVatOption?.IsCustom == true;

            Services = EnumText.Options<ServiceKind>(_localizer);
            Bases = EnumText.Options<ChargeBasis>(_localizer);
            SelectedService = service is null ? null : Services.First(o => o.Value.Equals(service));
            SelectedBasis = basis is null ? null : Bases.First(o => o.Value.Equals(basis));
            DeleteWholeText = _localizer["TariffDelete"];
            if (SelectedBasis is not null)
            {
                RebuildParameters(parameter);
                RebuildVatOptions(vat, vatCustom);
                RebuildRates();
                RelabelMeterAndRegisterSwitches();
            }

            Title = TitleText();
            foreach (var name in new[]
            {
                nameof(SelectedCategoryName), nameof(BasisHint), nameof(GeneralHeader), nameof(NameLabel), nameof(ServiceLabel), nameof(CategoryLabel),
                nameof(BasisLabel), nameof(NotesHeader), nameof(CountedHeader), nameof(MetersLabel), nameof(NoMetersText), nameof(RegistersLabel),
                nameof(ParameterLabel), nameof(CustomParameterLabel), nameof(VatHeader), nameof(VatPercentLabel), nameof(PricesHeader),
                nameof(PricesEmptyText), nameof(AddRateText), nameof(ExampleHeader),
            })
            {
                OnPropertyChanged(name);
            }

            RaiseExample();
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    private static int IndexOf(IReadOnlyList<EnumOption> options, EnumOption? selected) =>
        selected is null ? -1 : options.ToList().IndexOf(selected);

    private static EnumOption? Pick(IReadOnlyList<EnumOption> options, int index) =>
        index >= 0 && index < options.Count ? options[index] : null;

    private static string Join(params string[] messages) => string.Join("\n", messages.Where(m => m.Length > 0));

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private string TitleText() => IsNew ? _localizer["TariffComponentNew"] : _component.Name;

    private Currency CurrencyOfContract() => _contract.Currency;

    private TariffDescriber Describer() => new(_localizer, _clock.Today, _contract.Currency, _catalog, _property, _linkedMeters);

    private void RaiseExample()
    {
        OnPropertyChanged(nameof(Example));
        OnPropertyChanged(nameof(ExampleText));
        OnPropertyChanged(nameof(ParameterNowText));
    }

    private async Task LoadMetersAsync()
    {
        var links = await _contractStore.ListContractMetersAsync(ContractId);
        var today = _clock.Today;
        var wanted = links
            .Where(l => !l.IsDeleted && (l.ValidTo is null || l.ValidTo >= today))
            .Select(l => l.MeterId)
            .Concat(_component.MeterIds)
            .ToHashSet();
        var propertyMeters = _contract.PropertyId == Guid.Empty ? [] : await _meterStore.ListByPropertyAsync(_contract.PropertyId);
        _linkedMeters = [.. propertyMeters.Where(m => wanted.Contains(m.Id))];
    }

    private void BuildMeterSwitches()
    {
        _meterSwitches.Clear();
        foreach (var meter in _linkedMeters)
        {
            var isOn = _component.MeterIds.Count == 0 || _component.MeterIds.Contains(meter.Id);
            var row = new TariffMeterSwitchViewModel(meter.Id, MeterTitle(meter), isOn);
            row.Toggled = () =>
            {
                RebuildRegisters();
                MarkDirty();
                RebuildRates();
            };
            _meterSwitches.Add(row);
        }

        Meters = [.. _meterSwitches];
    }

    private string MeterTitle(Meter meter) =>
        _localizer.Get("TariffMeterTitle", MeterNames.Title(_localizer, meter), EnumText.Get(_localizer, meter.Resource));

    private void RelabelMeterAndRegisterSwitches()
    {
        var states = _meterSwitches.ToDictionary(s => s.MeterId, s => s.IsOn);
        _meterSwitches.Clear();
        foreach (var meter in _linkedMeters)
        {
            var row = new TariffMeterSwitchViewModel(meter.Id, MeterTitle(meter), states.GetValueOrDefault(meter.Id, true));
            row.Toggled = () =>
            {
                RebuildRegisters();
                MarkDirty();
                RebuildRates();
            };
            _meterSwitches.Add(row);
        }

        Meters = [.. _meterSwitches];
        RebuildRegisters();
    }

    /// <summary>The register kinds present on the selected meters (all import kinds when no meter is linked yet).</summary>
    private IReadOnlyList<RegisterKind> AvailableRegisterKinds()
    {
        if (Basis == ChargeBasis.NetExport)
        {
            return [RegisterKind.Export];
        }

        var selected = _meterSwitches.Where(s => s.IsOn).Select(s => s.MeterId).ToHashSet();
        var kinds = _linkedMeters
            .Where(m => selected.Contains(m.Id))
            .SelectMany(m => m.Registers)
            .Where(r => !r.IsDeleted && r.Direction == EnergyDirection.Import)
            .Select(r => r.Kind)
            .Distinct()
            .Order()
            .ToList();
        return kinds.Count > 0 ? kinds : [RegisterKind.Total, RegisterKind.Day, RegisterKind.Night, RegisterKind.Peak];
    }

    /// <summary>Rebuilds the register switches, keeping the state of kinds that are still offered.</summary>
    private void RebuildRegisters()
    {
        var states = _registerSwitches.ToDictionary(s => s.Kind, s => s.IsOn);
        var first = _registerSwitches.Count == 0;
        _registerSwitches.Clear();
        foreach (var kind in AvailableRegisterKinds())
        {
            var isOn = first
                ? _component.RegisterKinds.Count == 0 || _component.RegisterKinds.Contains(kind) || Basis == ChargeBasis.NetExport
                : states.GetValueOrDefault(kind, true);
            var row = new TariffRegisterSwitchViewModel(kind, EnumText.Get(_localizer, kind), isOn);
            row.Toggled = () =>
            {
                MarkDirty();
                RaiseExample();
            };
            _registerSwitches.Add(row);
        }

        Registers = [.. _registerSwitches];
    }

    private void RebuildParameters(object? keep = null)
    {
        var kinds = Basis == ChargeBasis.PerResident ? ResidentParameters : AreaParameters;
        Parameters = [.. kinds.Select(k => new EnumOption(k, EnumText.Get(_localizer, k)))];
        var wanted = keep as PropertyParameterKind?
            ?? (Basis == _component.Basis ? _component.QuantityParameter : null)
            ?? TariffDescriber.DefaultParameter(Basis);
        SelectedParameter = Parameters.FirstOrDefault(o => o.Value.Equals(wanted)) ?? Parameters[0];
        if (keep is null && Basis == _component.Basis)
        {
            CustomParameterName = _component.QuantityParameterName ?? string.Empty;
        }
    }

    private void RebuildVatOptions(string? keepCode = null, bool keepCustom = false)
    {
        var today = _clock.Today;
        var options = _catalog
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.Code, StringComparer.Ordinal)
            .Select(v => new VatOption(
                v.Code,
                v.PercentAt(today) is { } percent
                    ? _localizer.Get("TariffVatOption", v.Name, DecimalInput.Format(percent, CultureInfo.CurrentCulture, 2))
                    : v.Name,
                false))
            .ToList();

        var code = keepCode ?? _component.VatCode;
        var custom = keepCode is null ? _component.VatPercentOverride is not null : keepCustom;
        if (!custom && code.Length > 0 && options.All(o => o.Code != code))
        {
            options.Add(new VatOption(code, code, false));
        }

        options.Add(new VatOption(string.Empty, _localizer["TariffVatCustom"], true));
        VatOptions = options;

        if (custom)
        {
            SelectedVatOption = options[^1];
            if (keepCode is null && _component.VatPercentOverride is { } overridden)
            {
                VatPercentText = DecimalInput.Format(overridden, CultureInfo.CurrentCulture, 4);
            }
        }
        else
        {
            // The custom entry has an empty code too, so an empty code means "no choice yet": the standard rate.
            SelectedVatOption = (code.Length > 0 ? options.FirstOrDefault(o => o.Code == code) : null)
                ?? options.FirstOrDefault(o => o.Code == TariffPresets.StandardVatCode)
                ?? options[0];
        }
    }

    private void RebuildRates()
    {
        var describer = Describer();
        var draft = BuildComponent(record: false);
        foreach (var row in _rateRows)
        {
            row.DisplayText = _localizer.Get("TariffRateRow", row.Rate.ValidFrom, describer.PriceText(draft, row.Rate));
            row.SteppedText = _localizer["TariffSteppedBadge"];
            row.DeleteText = _localizer["Delete"];
        }

        Rates = [.. _rateRows.OrderByDescending(r => r.Rate.ValidFrom)];
        RaiseExample();
    }

    private TariffRateRowViewModel CreateRateRow(TariffRate rate)
    {
        var row = new TariffRateRowViewModel(rate);
        row.EditCommand = new RelayCommand(() => EditRateCommand.Execute(row));
        row.RemoveCommand = new RelayCommand(() => RemoveRateCommand.Execute(row));
        return row;
    }

    private bool IsDuplicateRate(TariffRate candidate, TariffRateRowViewModel? editing) =>
        _rateRows.Any(r => !ReferenceEquals(r, editing) && r.Rate.ValidFrom == candidate.ValidFrom);

    private IReadOnlyList<ChoiceOption> CategoryOptions() =>
    [
        .. _categories
            .Where(c => !c.IsArchived || c.Id == SelectedCategory?.Id)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChoiceOption(c.Id, CategoryNames.DisplayName(c, _localizer), c.Color)),
    ];

    private ChoiceOption? FindCategoryOption(Guid id) =>
        _categories.FirstOrDefault(c => c.Id == id) is { } category
            ? new ChoiceOption(category.Id, CategoryNames.DisplayName(category, _localizer), category.Color)
            : null;

    /// <summary>
    /// Builds the component from the form. With <paramref name="record"/> unparsable input is remembered as an error; without it
    /// (the live example) it is silently ignored.
    /// </summary>
    private TariffComponent BuildComponent(bool record)
    {
        var basis = Basis;
        var component = new TariffComponent
        {
            Id = _component.Id,
            CreatedAt = _component.CreatedAt,
            UpdatedAt = _component.UpdatedAt,
            ContractId = ContractId,
            Name = ComponentName.Trim(),
            ChargeKind = SelectedService?.As<ServiceKind>() ?? _contract.Service,
            CategoryId = SelectedCategory?.Id,
            Basis = basis,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            SortOrder = _component.SortOrder,
        };

        if (SelectedVatOption is { IsCustom: true })
        {
            component.VatCode = string.Empty;
            if (DecimalInput.TryParse(VatPercentText, CultureInfo.CurrentCulture, out var percent))
            {
                component.VatPercentOverride = percent;
            }
            else if (record)
            {
                AddInputError(CommonErrorCodes.NumberInvalid, nameof(TariffComponent.VatPercentOverride));
            }
        }
        else
        {
            component.VatCode = SelectedVatOption?.Code ?? string.Empty;
        }

        if (basis is ChargeBasis.PerUnit or ChargeBasis.NetExport)
        {
            ApplySelection(component, record);
        }
        else if (basis is ChargeBasis.PerArea or ChargeBasis.PerResident)
        {
            var kind = SelectedParameter?.As<PropertyParameterKind>() ?? TariffDescriber.DefaultParameter(basis);
            if (kind != TariffDescriber.DefaultParameter(basis))
            {
                component.QuantityParameter = kind;
            }

            if (kind == PropertyParameterKind.Custom)
            {
                component.QuantityParameterName = CustomParameterName.Trim();
            }
        }

        _buildOrder = [.. _rateRows.OrderBy(r => r.Rate.ValidFrom)];
        if (basis != ChargeBasis.FromBill)
        {
            foreach (var row in _buildOrder)
            {
                component.Rates.Add(row.Rate);
            }
        }

        return component;
    }

    /// <summary>All switches on mean "all" (stored as empty); a partial choice is stored explicitly; none is an error.</summary>
    private void ApplySelection(TariffComponent component, bool record)
    {
        if (_meterSwitches.Count > 0)
        {
            var on = _meterSwitches.Where(s => s.IsOn).Select(s => s.MeterId).ToList();
            if (on.Count == 0 && record)
            {
                AddInputError(CommonErrorCodes.TariffNoMeterSelected, nameof(TariffComponent.MeterIds));
            }
            else if (on.Count < _meterSwitches.Count)
            {
                component.MeterIds = on;
            }
        }

        if (component.Basis == ChargeBasis.NetExport)
        {
            if (_registerSwitches.Any(s => s.IsOn))
            {
                component.RegisterKinds = [RegisterKind.Export];
            }
            else if (record)
            {
                AddInputError(CommonErrorCodes.TariffNoRegisterSelected, nameof(TariffComponent.RegisterKinds));
            }

            return;
        }

        var kinds = _registerSwitches.Where(s => s.IsOn).Select(s => s.Kind).ToList();
        if (kinds.Count == 0 && record)
        {
            AddInputError(CommonErrorCodes.TariffNoRegisterSelected, nameof(TariffComponent.RegisterKinds));
        }
        else if (kinds.Count < _registerSwitches.Count)
        {
            component.RegisterKinds = kinds;
        }
    }

    private void AddInputError(string code, string field) => _inputErrors = [.. _inputErrors, new ValidationError(code, field)];
}
