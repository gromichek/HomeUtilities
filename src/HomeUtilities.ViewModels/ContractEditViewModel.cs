using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Editor of one contract of a property. It is a page in every layout; see <see cref="Routes.ContractEdit"/>.</summary>
public sealed partial class ContractEditViewModel : PaneEditorViewModel
{
    /// <summary>The "?" opens the section of the billing mode: equalized payments, property tax, else the contracts.</summary>
    protected override HelpTopic? GuideTopic => SelectedBillingMode?.Value switch
    {
        BillingMode.Equalized => HelpTopic.EqualizedPayments,
        BillingMode.Assessed => HelpTopic.PropertyTax,
        _ => HelpTopic.Contracts,
    };

    private const string DetailsPrefix = "PaymentDetails.";

    private static readonly Regex LinkErrorField = new(@"^Links\[(\d+)\]\.", RegexOptions.Compiled);
    private static readonly Regex CustomFieldErrorField = new(@"^CustomFields\[(\d+)\]\.", RegexOptions.Compiled);

    /// <summary>The properties that mark the editor dirty when the user changes them.</summary>
    private static readonly HashSet<string> DirtyFields =
    [
        nameof(ContractTitle), nameof(SelectedService), nameof(SelectedCategory), nameof(SelectedSupplier), nameof(SelectedCurrency),
        nameof(IsArchived), nameof(CustomerNumber), nameof(ContractNumber), nameof(PortalLogin), nameof(ValidFrom),
        nameof(HasEndDate), nameof(ValidTo), nameof(SelectedBillingMode), nameof(SelectedBillingTiming), nameof(VatOnTotal), nameof(HasReadingWindow), nameof(RemindAboutReadings),
        nameof(ReadingDayFromText), nameof(ReadingDayToText), nameof(BillDayText), nameof(PaymentDueDayText),
        nameof(RecipientName), nameof(RecipientRegistrationNumber), nameof(IbanText), nameof(Bic), nameof(BankName),
        nameof(PurposeTemplate), nameof(Notes),
    ];

    private readonly IContractStore _store;
    private readonly ISupplierStore _supplierStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IPropertyStore _propertyStore;
    private readonly ITariffStore _tariffStore;
    private readonly IVatRateStore _vatStore;
    private readonly IMeterStore _meterStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly ILauncherService _launcher;
    private readonly IClipboardService _clipboard;
    private readonly ISnackbarService _snackbar;
    private readonly Func<SupplierEditViewModel> _supplierEditorFactory;
    private readonly List<LinkRowViewModel> _linkRows = [];
    private readonly List<CustomFieldRowViewModel> _fieldRows = [];

    private Contract _contract = new();
    private Contract? _pending;
    private Guid _propertyId;
    private string _propertyName = string.Empty;
    private Property? _property;
    private IReadOnlyList<TariffComponent> _components = [];
    private IReadOnlyList<VatRate> _catalog = [];
    private IReadOnlyList<Meter> _linkedMeters = [];
    private IReadOnlyList<Supplier> _suppliers = [];
    private IReadOnlyList<Category> _categories = [];
    private bool _categoryTouched;
    private bool _hasLegacyCurrency;
    private IReadOnlyList<ValidationError> _inputErrors = [];

    [ObservableProperty]
    private string _contractTitle = string.Empty;

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
    [NotifyPropertyChangedFor(
        nameof(SelectedSupplierName), nameof(EffectiveRecipient), nameof(EffectiveIban), nameof(EffectivePurpose),
        nameof(HasEffectiveRecipient), nameof(HasEffectiveIban), nameof(HasEffectivePurpose), nameof(RecipientNamePlaceholder),
        nameof(RecipientRegistrationNumberPlaceholder), nameof(IbanPlaceholder), nameof(BicPlaceholder), nameof(BankNamePlaceholder),
        nameof(PurposeTemplatePlaceholder))]
    private ChoiceOption? _selectedSupplier;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCurrencyIndex))]
    private IReadOnlyList<EnumOption> _currencies = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCurrencyIndex))]
    private EnumOption? _selectedCurrency;

    [ObservableProperty]
    private bool _isArchived;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectivePurpose), nameof(HasEffectivePurpose))]
    private string _customerNumber = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectivePurpose), nameof(HasEffectivePurpose))]
    private string _contractNumber = string.Empty;

    [ObservableProperty]
    private string _portalLogin = string.Empty;

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private bool _hasEndDate;

    [ObservableProperty]
    private DateOnly _validTo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBillingModeIndex))]
    private IReadOnlyList<EnumOption> _billingModes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBillingModeIndex))]
    private EnumOption? _selectedBillingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBillingTimingIndex))]
    private IReadOnlyList<EnumOption> _billingTimings = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBillingTimingIndex), nameof(BillingTimingHint), nameof(EffectivePurpose), nameof(HasEffectivePurpose))]
    private EnumOption? _selectedBillingTiming;

    [ObservableProperty]
    private bool _vatOnTotal = true;

    [ObservableProperty]
    private bool _hasReadingWindow;

    [ObservableProperty]
    private bool _remindAboutReadings = true;

    [ObservableProperty]
    private string _remindAboutReadingsLabel = string.Empty;

    [ObservableProperty]
    private string _readingDayFromText = string.Empty;

    [ObservableProperty]
    private string _readingDayToText = string.Empty;

    [ObservableProperty]
    private string _billDayText = string.Empty;

    [ObservableProperty]
    private string _paymentDueDayText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoOwnLinks), nameof(HasNoLinks))]
    private IReadOnlyList<LinkRowViewModel> _links = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInheritedLinks), nameof(HasNoLinks))]
    private IReadOnlyList<InheritedLinkRowViewModel> _inheritedLinks = [];

    [ObservableProperty]
    private string _recipientName = string.Empty;

    [ObservableProperty]
    private string _recipientRegistrationNumber = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveIban), nameof(HasEffectiveIban))]
    private string _ibanText = string.Empty;

    [ObservableProperty]
    private string _bic = string.Empty;

    [ObservableProperty]
    private string _bankName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectivePurpose), nameof(HasEffectivePurpose))]
    private string _purposeTemplate = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCustomFields))]
    private IReadOnlyList<CustomFieldRowViewModel> _customFields = [];

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _generalHeader = string.Empty;

    [ObservableProperty]
    private string _contractTitleLabel = string.Empty;

    [ObservableProperty]
    private string _serviceLabel = string.Empty;

    [ObservableProperty]
    private string _categoryLabel = string.Empty;

    [ObservableProperty]
    private string _supplierLabel = string.Empty;

    [ObservableProperty]
    private string _currencyLabel = string.Empty;

    [ObservableProperty]
    private string _archivedLabel = string.Empty;

    [ObservableProperty]
    private string _accountHeader = string.Empty;

    [ObservableProperty]
    private string _customerNumberLabel = string.Empty;

    [ObservableProperty]
    private string _contractNumberLabel = string.Empty;

    [ObservableProperty]
    private string _portalLoginLabel = string.Empty;

    [ObservableProperty]
    private string _portalLoginHint = string.Empty;

    [ObservableProperty]
    private string _validFromLabel = string.Empty;

    [ObservableProperty]
    private string _hasEndDateLabel = string.Empty;

    [ObservableProperty]
    private string _validToLabel = string.Empty;

    [ObservableProperty]
    private string _billingHeader = string.Empty;

    [ObservableProperty]
    private string _billingModeLabel = string.Empty;

    [ObservableProperty]
    private string _billingTimingLabel = string.Empty;

    [ObservableProperty]
    private string _readingWindowLabel = string.Empty;

    [ObservableProperty]
    private string _readingFromLabel = string.Empty;

    [ObservableProperty]
    private string _readingToLabel = string.Empty;

    [ObservableProperty]
    private string _readingWindowHint = string.Empty;

    [ObservableProperty]
    private string _vatOnTotalLabel = string.Empty;

    [ObservableProperty]
    private string _vatOnTotalHint = string.Empty;

    [ObservableProperty]
    private string _billDayLabel = string.Empty;

    [ObservableProperty]
    private string _paymentDueDayLabel = string.Empty;

    [ObservableProperty]
    private string _linksHeader = string.Empty;

    [ObservableProperty]
    private string _linksEmptyText = string.Empty;

    [ObservableProperty]
    private string _addLinkText = string.Empty;

    [ObservableProperty]
    private string _paymentHeader = string.Empty;

    [ObservableProperty]
    private string _paymentHintText = string.Empty;

    [ObservableProperty]
    private string _recipientNameLabel = string.Empty;

    [ObservableProperty]
    private string _recipientRegistrationNumberLabel = string.Empty;

    [ObservableProperty]
    private string _ibanLabel = string.Empty;

    [ObservableProperty]
    private string _bicLabel = string.Empty;

    [ObservableProperty]
    private string _bankNameLabel = string.Empty;

    [ObservableProperty]
    private string _purposeTemplateLabel = string.Empty;

    [ObservableProperty]
    private string _purposeHintText = string.Empty;

    [ObservableProperty]
    private string _effectiveHeader = string.Empty;

    [ObservableProperty]
    private string _effectiveRecipientLabel = string.Empty;

    [ObservableProperty]
    private string _effectiveIbanLabel = string.Empty;

    [ObservableProperty]
    private string _effectivePurposeLabel = string.Empty;

    [ObservableProperty]
    private string _copyIbanText = string.Empty;

    [ObservableProperty]
    private string _copyRecipientText = string.Empty;

    [ObservableProperty]
    private string _copyPurposeText = string.Empty;

    [ObservableProperty]
    private string _customFieldsHeader = string.Empty;

    [ObservableProperty]
    private string _customFieldsHintText = string.Empty;

    [ObservableProperty]
    private string _customFieldsEmptyText = string.Empty;

    [ObservableProperty]
    private string _addCustomFieldText = string.Empty;

    [ObservableProperty]
    private string _notesHeader = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTariff), nameof(ShowTariffPresets))]
    private IReadOnlyList<TariffRowViewModel> _tariffRows = [];

    [ObservableProperty]
    private IReadOnlyList<TariffPresetRowViewModel> _tariffPresetRows = [];

    [ObservableProperty]
    private string _tariffHeader = string.Empty;

    [ObservableProperty]
    private string _tariffEmptyText = string.Empty;

    [ObservableProperty]
    private string _addTariffText = string.Empty;

    [ObservableProperty]
    private string _showChargesText = string.Empty;

    [ObservableProperty]
    private string _tariffPresetsLabel = string.Empty;

    [ObservableProperty]
    private string _tariffSaveFirstText = string.Empty;

    public ContractEditViewModel(
        IContractStore store,
        ISupplierStore supplierStore,
        ICategoryStore categoryStore,
        IPropertyStore propertyStore,
        ITariffStore tariffStore,
        IVatRateStore vatStore,
        IMeterStore meterStore,
        IEqualizedPlanStore planStore,
        EqualizedSettlementService settlementService,
        IBillStore billStore,
        IPaymentStore paymentStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ILauncherService launcher,
        IClipboardService clipboard,
        ISnackbarService snackbar,
        PrepaymentService prepayments,
        BillTemplateLauncher templateLauncher,
        Func<SupplierEditViewModel> supplierEditorFactory)
        : base(localizer, dialogs, navigation, snackbar)
    {
        _store = store;
        _supplierStore = supplierStore;
        _categoryStore = categoryStore;
        _propertyStore = propertyStore;
        _tariffStore = tariffStore;
        _vatStore = vatStore;
        _meterStore = meterStore;
        _planStore = planStore;
        _settlementService = settlementService;
        _billStore = billStore;
        _paymentStore = paymentStore;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _launcher = launcher;
        _clipboard = clipboard;
        _snackbar = snackbar;
        _prepayments = prepayments;
        _templateLauncher = templateLauncher;
        _supplierEditorFactory = supplierEditorFactory;
        ValidFrom = clock.Today;
        ValidTo = clock.Today;
        RefreshTexts();
    }

    public override Guid EntityId => _contract.Id;

    /// <summary>Id of the property the contract belongs to; set before <see cref="LoadAsync(Guid?)"/> for a new contract.</summary>
    public Guid PropertyId
    {
        get => _propertyId;
        set => _propertyId = value;
    }

    public string SelectedCategoryName => SelectedCategory?.Name ?? string.Empty;

    public string SelectedCategoryColor => SelectedCategory?.Color ?? string.Empty;

    public bool HasCategoryColor => SelectedCategoryColor.Length > 0;

    public string SelectedSupplierName => SelectedSupplier?.Name ?? string.Empty;

    public bool HasNoOwnLinks => Links.Count == 0;

    public bool HasInheritedLinks => InheritedLinks.Count > 0;

    public bool HasNoLinks => Links.Count == 0 && InheritedLinks.Count == 0;

    public bool HasNoCustomFields => CustomFields.Count == 0;

    public bool HasNoTariff => TariffRows.Count == 0;

    /// <summary>Components are added to a saved contract only (they belong to it).</summary>
    public bool CanAddTariff => !IsNew;

    /// <summary>The quick-start presets are offered while a saved contract has no components.</summary>
    public bool ShowTariffPresets => CanAddTariff && HasNoTariff;

    /// <summary>"Save the contract first" hint instead of the add button.</summary>
    public bool ShowTariffSaveFirst => IsNew;

    /// <summary>LVL is offered only for a contract that already uses it.</summary>
    public bool ShowCurrencyPicker => Currencies.Count > 1;

    public int SelectedServiceIndex
    {
        get => IndexOf(Services, SelectedService);
        set => SelectedService = Pick(Services, value) ?? SelectedService;
    }

    public int SelectedCurrencyIndex
    {
        get => IndexOf(Currencies, SelectedCurrency);
        set => SelectedCurrency = Pick(Currencies, value) ?? SelectedCurrency;
    }

    public int SelectedBillingModeIndex
    {
        get => IndexOf(BillingModes, SelectedBillingMode);
        set => SelectedBillingMode = Pick(BillingModes, value) ?? SelectedBillingMode;
    }

    public int SelectedBillingTimingIndex
    {
        get => IndexOf(BillingTimings, SelectedBillingTiming);
        set => SelectedBillingTiming = Pick(BillingTimings, value) ?? SelectedBillingTiming;
    }

    /// <summary>One-line explanation of the selected billing timing.</summary>
    public string BillingTimingHint => _localizer["ContractTimingHint_" + (SelectedBillingTiming?.Value ?? BillingTiming.InArrears)];

    public string ContractTitleError => Lookup(nameof(Contract.Title));

    public string CategoryError => Lookup(nameof(Contract.CategoryId));

    public string SupplierError => Lookup(nameof(Contract.SupplierId));

    public string ValidToError => Lookup(nameof(Contract.ValidTo));

    public string ReadingWindowError => string.Join(
        "\n",
        new[] { Lookup(nameof(Contract.ReadingDayFrom)), Lookup(nameof(Contract.ReadingDayTo)) }.Where(m => m.Length > 0));

    public string BillDayError => Lookup(nameof(Contract.BillDay));

    public string PaymentDueDayError => Lookup(nameof(Contract.PaymentDueDay));

    public string NotesError => Lookup(nameof(Contract.Notes));

    public string RecipientNameError => Lookup(DetailsPrefix + nameof(PaymentDetails.RecipientName));

    public string RecipientRegistrationNumberError => Lookup(DetailsPrefix + nameof(PaymentDetails.RecipientRegistrationNumber));

    public string IbanError => Lookup(DetailsPrefix + nameof(PaymentDetails.Iban));

    public string BicError => Lookup(DetailsPrefix + nameof(PaymentDetails.Bic));

    public string BankNameError => Lookup(DetailsPrefix + nameof(PaymentDetails.BankName));

    public string PurposeTemplateError => Lookup(DetailsPrefix + nameof(PaymentDetails.PurposeTemplate));

    /// <summary>Placeholder of the recipient field: the supplier's value, or empty when the supplier has none.</summary>
    public string RecipientNamePlaceholder => FromSupplier(SupplierDetails.RecipientName);

    public string RecipientRegistrationNumberPlaceholder => FromSupplier(SupplierDetails.RecipientRegistrationNumber);

    public string IbanPlaceholder => FromSupplier(SupplierDetails.Iban is { Length: > 0 } iban ? Iban.Format(iban) : null);

    public string BicPlaceholder => FromSupplier(SupplierDetails.Bic);

    public string BankNamePlaceholder => FromSupplier(SupplierDetails.BankName);

    public string PurposeTemplatePlaceholder => FromSupplier(SupplierDetails.PurposeTemplate);

    public string EffectiveRecipient => Effective().RecipientName ?? string.Empty;

    public string EffectiveIban => Effective().Iban is { Length: > 0 } iban ? Iban.Format(iban) : string.Empty;

    /// <summary>The effective purpose template rendered with the real contract data (customer, contract, period, property).</summary>
    public string EffectivePurpose => RenderPurpose(Effective().PurposeTemplate);

    public bool HasEffectiveRecipient => EffectiveRecipient.Length > 0;

    public bool HasEffectiveIban => EffectiveIban.Length > 0;

    public bool HasEffectivePurpose => EffectivePurpose.Length > 0;

    private PaymentDetails SupplierDetails => SelectedSupplierEntity?.PaymentDetails ?? PaymentDetails.Empty;

    private Supplier? SelectedSupplierEntity =>
        SelectedSupplier is { } option ? _suppliers.FirstOrDefault(s => s.Id == option.Id) : null;

    public Task LoadAsync(Guid propertyId, Guid? id)
    {
        PropertyId = propertyId;
        return LoadAsync(id);
    }

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var existing = id is { } value ? await _store.GetAsync(value) : null;
            _contract = existing ?? new Contract { PropertyId = _propertyId, ValidFrom = _clock.Today };
            _propertyId = _contract.PropertyId;
            IsNew = existing is null;

            _property = await _propertyStore.GetAsync(_propertyId);
            _propertyName = _property?.Name ?? string.Empty;
            _suppliers = await _supplierStore.ListAsync();
            _categories = await _categoryStore.ListAsync();
            _hasLegacyCurrency = _contract.Currency != Currency.EUR;
            _categoryTouched = !IsNew;

            RebuildOptions();
            SelectedService = Services.First(o => o.Value.Equals(_contract.Service));
            SelectedCurrency = Currencies.First(o => o.Value.Equals(_contract.Currency));
            SelectedBillingMode = BillingModes.First(o => o.Value.Equals(_contract.BillingMode));
            SelectedBillingTiming = BillingTimings.First(o => o.Value.Equals(_contract.BillingTiming));
            SelectedSupplier = FindSupplierOption(_contract.SupplierId);
            SelectedCategory = FindCategoryOption(_contract.CategoryId);
            if (IsNew)
            {
                PreselectCategory();
            }

            ContractTitle = _contract.Title;
            IsArchived = _contract.IsArchived;
            CustomerNumber = _contract.CustomerNumber ?? string.Empty;
            ContractNumber = _contract.ContractNumber ?? string.Empty;
            PortalLogin = _contract.PortalLogin ?? string.Empty;
            ValidFrom = _contract.ValidFrom;
            HasEndDate = _contract.ValidTo is not null;
            ValidTo = _contract.ValidTo ?? (_contract.ValidFrom > _clock.Today ? _contract.ValidFrom : _clock.Today);
            VatOnTotal = _contract.VatCalculation == VatCalculation.OnTotal;
            HasReadingWindow = _contract.ReadingDayFrom is not null || _contract.ReadingDayTo is not null;
            RemindAboutReadings = _contract.RemindAboutReadings;
            ReadingDayFromText = _contract.ReadingDayFrom?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            ReadingDayToText = _contract.ReadingDayTo?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            BillDayText = _contract.BillDay?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            PaymentDueDayText = _contract.PaymentDueDay?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            Notes = _contract.Notes ?? string.Empty;

            var details = _contract.PaymentDetails;
            RecipientName = details?.RecipientName ?? string.Empty;
            RecipientRegistrationNumber = details?.RecipientRegistrationNumber ?? string.Empty;
            IbanText = details?.Iban is { Length: > 0 } iban ? Iban.Format(iban) : string.Empty;
            Bic = details?.Bic ?? string.Empty;
            BankName = details?.BankName ?? string.Empty;
            PurposeTemplate = details?.PurposeTemplate ?? string.Empty;

            _linkRows.Clear();
            foreach (var link in _contract.Links.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder))
            {
                _linkRows.Add(CreateLinkRow(link));
            }

            _fieldRows.Clear();
            foreach (var field in _contract.CustomFields.Where(f => !f.IsDeleted).OrderBy(f => f.SortOrder))
            {
                _fieldRows.Add(CreateFieldRow(field));
            }

            await LoadTariffAsync();
            await LoadBillsAsync();
            await LoadEqualizedAsync();
            await LoadTaxAsync();
            ClearErrors();
            RebuildLinks();
            RebuildFields();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    /// <summary>Back from the component editor: its changes are saved on their own, so the card is re-read.</summary>
    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();
        if (!IsNew)
        {
            await LoadTariffAsync();
            await LoadBillsAsync();
            await LoadEqualizedAsync();
            await LoadTaxAsync();
        }
    }

    /// <summary>Opens the charges screen on this contract's property and the default month.</summary>
    [RelayCommand(CanExecute = nameof(CanAddTariff))]
    private Task ShowChargesAsync() => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.ChargesFor,
        new Dictionary<string, object>
        {
            [EditorNavigation.PropertyIdParameter] = _contract.PropertyId,
            [EditorNavigation.ContractIdParameter] = _contract.Id,
        }));

    [RelayCommand(CanExecute = nameof(CanAddTariff))]
    private Task AddTariffAsync() => RunSafeAsync(() => OpenComponentAsync(null));

    private Task OpenComponentAsync(Guid? id)
    {
        var parameters = new Dictionary<string, object> { [EditorNavigation.ContractIdParameter] = _contract.Id };
        if (id is { } value)
        {
            parameters[EditorNavigation.IdParameter] = value;
        }

        return _navigation.GoToAsync(Routes.TariffComponentEdit, parameters);
    }

    /// <summary>Creates the components of a quick-start preset (without prices); only while the card is empty.</summary>
    [RelayCommand]
    private Task ApplyTariffPresetAsync(TariffPreset preset) => RunSafeAsync(async () =>
    {
        if (IsNew || _components.Count > 0)
        {
            return;
        }

        var hot = _linkedMeters.Where(m => m.Resource == MeterResource.HotWater).Select(m => m.Id).ToList();
        var service = SelectedService?.As<ServiceKind>() ?? _contract.Service;
        foreach (var component in TariffPresets.Create(preset, _contract.Id, service, key => _localizer["TariffPresetName_" + key], hot))
        {
            await _tariffStore.SaveAsync(component);
        }

        await LoadTariffAsync();
    });

    private Task MoveTariffAsync(TariffRowViewModel row, int direction) => RunSafeAsync(async () =>
    {
        var ordered = _components.OrderBy(c => c.SortOrder).ToList();
        var index = ordered.FindIndex(c => c.Id == row.Id);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].SortOrder != i)
            {
                ordered[i].SortOrder = i;
                await _tariffStore.SaveAsync(ordered[i]);
            }
        }

        _components = ordered;
        RebuildTariff();
    });

    private async Task LoadTariffAsync()
    {
        if (IsNew)
        {
            _components = [];
            _linkedMeters = [];
        }
        else
        {
            _components = await _tariffStore.ListByContractAsync(_contract.Id);
            _catalog = await _vatStore.ListAsync();
            var links = await _store.ListContractMetersAsync(_contract.Id);
            var wanted = links
                .Where(l => !l.IsDeleted && (l.ValidTo is null || l.ValidTo >= _clock.Today))
                .Select(l => l.MeterId)
                .ToHashSet();
            var propertyMeters = await _meterStore.ListByPropertyAsync(_propertyId);
            _linkedMeters = [.. propertyMeters.Where(m => wanted.Contains(m.Id))];
        }

        RebuildTariff();
    }

    private void RebuildTariff()
    {
        var describer = new TariffDescriber(
            _localizer, _clock.Today, SelectedCurrency?.As<Currency>() ?? _contract.Currency, _catalog, _property, _linkedMeters);
        var ordered = _components.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var rows = new List<TariffRowViewModel>();
        foreach (var component in ordered)
        {
            var row = new TariffRowViewModel(component, EnumText.Get(_localizer, component.Basis), describer.Summary(component))
            {
                MoveUpText = _localizer["TariffMoveUp"],
                MoveDownText = _localizer["TariffMoveDown"],
            };
            row.CanMoveUp = rows.Count > 0;
            row.EditCommand = new RelayCommand(() => _ = RunSafeAsync(() => OpenComponentAsync(row.Id)));
            row.MoveUpCommand = new RelayCommand(() => _ = MoveTariffAsync(row, -1));
            row.MoveDownCommand = new RelayCommand(() => _ = MoveTariffAsync(row, 1));
            rows.Add(row);
        }

        foreach (var row in rows)
        {
            row.CanMoveDown = row != rows[^1];
        }

        TariffRows = rows;
        TariffPresetRows =
        [
            .. Enum.GetValues<TariffPreset>().Select(p => new TariffPresetRowViewModel(
                p, _localizer["TariffPreset_" + p], new RelayCommand(() => _ = ApplyTariffPresetCommand.ExecuteAsync(p)))),
        ];
        OnPropertyChanged(nameof(ShowTariffPresets));
    }

    /// <summary>Opens the category list; the chosen category becomes the user's own choice.</summary>
    [RelayCommand]
    private Task ChooseCategoryAsync() => RunSafeAsync(async () =>
    {
        var options = CategoryOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["ContractChooseCategory"], [.. options.Select(o => o.Name)]);
        if (picked is null || options.FirstOrDefault(o => o.Name == picked) is not { } option)
        {
            return;
        }

        _categoryTouched = true;
        SelectedCategory = option;
    });

    /// <summary>Opens the supplier list with "＋ New supplier…" as the last entry.</summary>
    [RelayCommand]
    private Task ChooseSupplierAsync() => RunSafeAsync(async () =>
    {
        var options = SupplierOptions();
        var newSupplier = _localizer["ContractNewSupplier"];
        var picked = await Dialogs.ChooseAsync(_localizer["ContractChooseSupplier"], [.. options.Select(o => o.Name), newSupplier]);
        if (picked is null)
        {
            return;
        }

        if (picked == newSupplier)
        {
            await NewSupplierAsync();
        }
        else if (options.FirstOrDefault(o => o.Name == picked) is { } option)
        {
            SelectedSupplier = option;
        }
    });

    /// <summary>
    /// Opens the supplier editor as a page; when the supplier was saved the list is re-read and the new supplier
    /// becomes the selected one. Anything else leaves the selection alone.
    /// </summary>
    [RelayCommand]
    private Task NewSupplierAsync() => RunSafeAsync(async () =>
    {
        var editor = _supplierEditorFactory();
        editor.IsHostedInPage = true;
        editor.Finished = async outcome =>
        {
            if (outcome != EditOutcome.Saved)
            {
                return;
            }

            _suppliers = await _supplierStore.ListAsync();
            SelectedSupplier = FindSupplierOption(editor.EntityId);
            RebuildLinks();
        };
        await editor.LoadAsync(null);
        await _navigation.GoToAsync(Routes.SupplierEdit, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = editor });
    });

    /// <summary>Groups the typed IBAN by four characters; bound to the field losing focus.</summary>
    [RelayCommand]
    private void FormatIban()
    {
        var formatted = Iban.Format(IbanText);
        if (formatted == IbanText)
        {
            return;
        }

        // Regrouping only changes how the same IBAN is shown, so it is not an edit.
        var previous = SuppressDirty;
        SuppressDirty = Iban.Normalize(formatted) == Iban.Normalize(IbanText);
        try
        {
            IbanText = formatted;
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    [RelayCommand]
    private Task CopyIbanAsync() => RunSafeAsync(() => CopyAsync(Iban.Normalize(EffectiveIban)));

    [RelayCommand]
    private Task CopyRecipientAsync() => RunSafeAsync(() => CopyAsync(EffectiveRecipient.Trim()));

    [RelayCommand]
    private Task CopyPurposeAsync() => RunSafeAsync(() => CopyAsync(EffectivePurpose));

    [RelayCommand]
    private Task AddLinkAsync() => RunSafeAsync(async () =>
    {
        var editor = new LinkEditorViewModel(_localizer, null);
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        _linkRows.Add(CreateLinkRow(result));
        RebuildLinks();
        MarkDirty();
    });

    [RelayCommand]
    private Task EditLinkAsync(LinkRowViewModel row) => RunSafeAsync(async () =>
    {
        var current = new Link { Kind = row.Kind, Title = row.Title, Url = row.Url };
        var editor = new LinkEditorViewModel(_localizer, current);
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        row.Kind = result.Kind;
        row.Title = result.Title;
        row.Url = result.Url;
        row.ErrorText = string.Empty;
        RebuildLinks();
        MarkDirty();
    });

    [RelayCommand]
    private Task RemoveLinkAsync(LinkRowViewModel row) => RunSafeAsync(async () =>
    {
        if (!await ConfirmRowDeleteAsync(row.DisplayText))
        {
            return;
        }

        _linkRows.Remove(row);
        RebuildLinks();
        MarkDirty();
    });

    /// <summary>Opens the link in the system browser; a URL that is not http/https shows the validation message instead.</summary>
    [RelayCommand]
    private Task OpenLinkAsync(LinkRowViewModel row) => RunSafeAsync(async () =>
    {
        var uri = new Link { Url = row.Url.Trim() }.TryGetUri();
        if (uri is null)
        {
            row.ErrorText = ValidationText.For(_localizer, new ValidationError(ErrorCodes.LinkUrlInvalid, nameof(Link.Url)));
            return;
        }

        row.ErrorText = string.Empty;
        await _launcher.OpenAsync(uri);
    });

    [RelayCommand]
    private Task AddCustomFieldAsync() => RunSafeAsync(async () =>
    {
        var editor = new CustomFieldEditorViewModel(_localizer, null, candidate => IsDuplicateField(candidate, null));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        _fieldRows.Add(CreateFieldRow(result));
        RebuildFields();
        MarkDirty();
    });

    [RelayCommand]
    private Task EditCustomFieldAsync(CustomFieldRowViewModel row) => RunSafeAsync(async () =>
    {
        var current = new CustomFieldDefinition { Name = row.Name, Kind = row.Kind, Target = row.Target };
        var editor = new CustomFieldEditorViewModel(_localizer, current, candidate => IsDuplicateField(candidate, row));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        row.Name = result.Name;
        row.Kind = result.Kind;
        row.Target = result.Target;
        row.ErrorText = string.Empty;
        RebuildFields();
        MarkDirty();
    });

    [RelayCommand]
    private Task RemoveCustomFieldAsync(CustomFieldRowViewModel row) => RunSafeAsync(async () =>
    {
        if (!await ConfirmRowDeleteAsync(row.DisplayText))
        {
            return;
        }

        _fieldRows.Remove(row);
        RebuildFields();
        MarkDirty();
    });

    protected override IReadOnlyList<ValidationError> Validate()
    {
        _inputErrors = [];
        _pending = BuildContract();
        return [.. _inputErrors, .. ContractValidator.Validate(_pending).Errors];
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildContract();
        await _store.SaveAsync(pending);
        _contract = pending;
        IsNew = false;
        Title = TitleText();
        FormatIban();
        await LoadBillsAsync();
        await LoadEqualizedAsync();
        await LoadTaxAsync();
    }

    protected override Task DeleteCoreAsync() => _store.DeleteAsync(_contract.Id);

    protected override string DeleteName() => _contract.Title;

    protected override void OnErrorsChanged()
    {
        foreach (var name in new[]
        {
            nameof(ContractTitleError), nameof(CategoryError), nameof(SupplierError), nameof(ValidToError), nameof(ReadingWindowError),
            nameof(BillDayError), nameof(PaymentDueDayError), nameof(NotesError), nameof(RecipientNameError),
            nameof(RecipientRegistrationNumberError), nameof(IbanError), nameof(BicError), nameof(BankNameError), nameof(PurposeTemplateError),
        })
        {
            OnPropertyChanged(name);
        }

        foreach (var row in _linkRows)
        {
            row.ErrorText = string.Empty;
        }

        foreach (var row in _fieldRows)
        {
            row.ErrorText = string.Empty;
        }

        foreach (var (field, message) in Errors)
        {
            if (RowAt(LinkErrorField, field, _linkRows) is { } link)
            {
                link.ErrorText = link.ErrorText.Length == 0 ? message : link.ErrorText + "\n" + message;
            }
            else if (RowAt(CustomFieldErrorField, field, _fieldRows) is { } custom)
            {
                custom.ErrorText = custom.ErrorText.Length == 0 ? message : custom.ErrorText + "\n" + message;
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

        if (name == nameof(SelectedBillingMode))
        {
            OnPropertyChanged(nameof(ShowEqualizedCard));
            OnPropertyChanged(nameof(ShowTaxCard));
        }

        if (name == nameof(IsNew))
        {
            OnPropertyChanged(nameof(CanEditPlans));
            OnPropertyChanged(nameof(ShowPlanSaveFirst));
            OnPropertyChanged(nameof(ShowPlanEmpty));
            OnPropertyChanged(nameof(ShowTaxEmpty));
            OnPropertyChanged(nameof(CanEditTax));
            OnPropertyChanged(nameof(ShowTaxSaveFirst));
            OnPropertyChanged(nameof(CanAddTariff));
            OnPropertyChanged(nameof(ShowTariffPresets));
            OnPropertyChanged(nameof(ShowTariffSaveFirst));
            AddTariffCommand.NotifyCanExecuteChanged();
            ShowChargesCommand.NotifyCanExecuteChanged();
            if (!IsNew && !SuppressDirty && _components.Count == 0)
            {
                _ = RunSafeAsync(LoadTariffAsync);
            }
        }
    }

    partial void OnSelectedServiceChanged(EnumOption? value)
    {
        if (!_categoryTouched && !SuppressDirty)
        {
            PreselectCategory();
        }
    }

    partial void OnSelectedSupplierChanged(ChoiceOption? value) => RebuildLinks();

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var service = SelectedService?.Value ?? default(ServiceKind);
            var mode = SelectedBillingMode?.Value ?? default(BillingMode);
            var timing = SelectedBillingTiming?.Value ?? default(BillingTiming);
            var currency = SelectedCurrency?.Value ?? default(Currency);
            var categoryId = SelectedCategory?.Id;
            var supplierId = SelectedSupplier?.Id;

            RebuildOptions();
            SelectedService = Services.First(o => o.Value.Equals(service));
            SelectedBillingMode = BillingModes.First(o => o.Value.Equals(mode));
            SelectedBillingTiming = BillingTimings.First(o => o.Value.Equals(timing));
            SelectedCurrency = Currencies.First(o => o.Value.Equals(currency));
            SelectedCategory = categoryId is { } c ? FindCategoryOption(c) : null;
            SelectedSupplier = supplierId is { } s ? FindSupplierOption(s) : null;

            GeneralHeader = _localizer["ContractGeneral"];
            ContractTitleLabel = _localizer["ContractTitleLabel"];
            ServiceLabel = _localizer["ContractServiceLabel"];
            CategoryLabel = _localizer["ContractCategoryLabel"];
            SupplierLabel = _localizer["ContractSupplierLabel"];
            CurrencyLabel = _localizer["ContractCurrencyLabel"];
            ArchivedLabel = _localizer["ContractArchivedLabel"];
            AccountHeader = _localizer["ContractAccount"];
            CustomerNumberLabel = _localizer["ContractCustomerNumber"];
            ContractNumberLabel = _localizer["ContractContractNumber"];
            PortalLoginLabel = _localizer["ContractPortalLogin"];
            PortalLoginHint = _localizer["ContractPortalLoginHint"];
            ValidFromLabel = _localizer["ContractValidFrom"];
            HasEndDateLabel = _localizer["ContractHasEndDate"];
            ValidToLabel = _localizer["ContractValidTo"];
            BillingHeader = _localizer["ContractBilling"];
            BillingModeLabel = _localizer["ContractBillingModeLabel"];
            BillingTimingLabel = _localizer["ContractBillingTimingLabel"];
            VatOnTotalLabel = _localizer["ContractVatOnTotal"];
            VatOnTotalHint = _localizer["ContractVatOnTotalHint"];
            ReadingWindowLabel = _localizer["ContractReadingWindow"];
            RemindAboutReadingsLabel = _localizer["ContractRemindAboutReadings"];
            ReadingFromLabel = _localizer["ContractReadingFrom"];
            ReadingToLabel = _localizer["ContractReadingTo"];
            ReadingWindowHint = _localizer["ContractReadingHint"];
            BillDayLabel = _localizer["ContractBillDay"];
            PaymentDueDayLabel = _localizer["ContractPaymentDueDay"];
            LinksHeader = _localizer["LinksHeader"];
            LinksEmptyText = _localizer["LinksEmpty"];
            AddLinkText = _localizer["LinkAdd"];
            PaymentHeader = _localizer["PaymentDetailsHeader"];
            PaymentHintText = _localizer["ContractPaymentHint"];
            RecipientNameLabel = _localizer["PaymentRecipient"];
            RecipientRegistrationNumberLabel = _localizer["PaymentRecipientRegistrationNumber"];
            IbanLabel = _localizer["PaymentIban"];
            BicLabel = _localizer["PaymentBic"];
            BankNameLabel = _localizer["PaymentBank"];
            PurposeTemplateLabel = _localizer["PaymentPurposeTemplate"];
            PurposeHintText = _localizer["PaymentPurposeHint"];
            EffectiveHeader = _localizer["ContractEffectiveHeader"];
            EffectiveRecipientLabel = _localizer["PaymentRecipient"];
            EffectiveIbanLabel = _localizer["PaymentIban"];
            EffectivePurposeLabel = _localizer["ContractEffectivePurpose"];
            CopyIbanText = _localizer["PaymentCopyIban"];
            CopyRecipientText = _localizer["PaymentCopyRecipient"];
            CopyPurposeText = _localizer["PaymentCopyPurpose"];
            CustomFieldsHeader = _localizer["CustomFieldsHeader"];
            CustomFieldsHintText = _localizer["CustomFieldsHint"];
            CustomFieldsEmptyText = _localizer["CustomFieldsEmpty"];
            AddCustomFieldText = _localizer["CustomFieldAdd"];
            NotesHeader = _localizer["ContractNotes"];
            TariffHeader = _localizer["ContractTariff"];
            TariffEmptyText = _localizer["ContractTariffEmpty"];
            AddTariffText = _localizer["ContractTariffAdd"];
            ShowChargesText = _localizer["ContractShowCharges"];
            BillsHeader = _localizer["BillBillsCardHeader"];
            BillsEmptyText = _localizer["BillBillsCardEmpty"];
            AllBillsText = _localizer["BillBillsCardAll"];
            AddBillText = _localizer["BillBillsCardAdd"];
            TemplateFromContractText = _localizer["BillTemplateFromContract"];
            PlanHeader = _localizer["EqPlanHeader"];
            PlanHintText = _localizer["EqPlanHint"];
            PlanEmptyText = _localizer["EqPlanEmpty"];
            AddPlanText = _localizer["EqPlanAdd"];
            PlanSaveFirstText = _localizer["EqPlanSaveFirst"];
            ShowSettlementText = _localizer["EqShowSettlement"];
            TaxHeader = _localizer["TaxHeader"];
            TaxHintText = _localizer["TaxHint"];
            TaxEmptyText = _localizer["TaxEmpty"];
            AddTaxYearText = _localizer["TaxAddYear"];
            TaxSaveFirstText = _localizer["TaxSaveFirst"];
            BalanceHeader = _localizer["ContractBalanceHeader"];
            BalanceBilledLabel = _localizer["ContractBalanceBilled"];
            BalancePaidLabel = _localizer["ContractBalancePaid"];
            UseForOpenBillsText = _localizer["ContractBalanceUse"];
            PaymentsHeader = _localizer["ContractPaymentsHeader"];
            PaymentsEmptyText = _localizer["ContractPaymentsEmpty"];
            AllPaymentsText = _localizer["ContractPaymentsAll"];
            AddPaymentText = _localizer["ContractPaymentsAdd"];
            RebuildBills();
            RebuildBalance();
            RebuildEqualized();
            RebuildTax();
            TariffPresetsLabel = _localizer["ContractTariffPresets"];
            TariffSaveFirstText = _localizer["ContractTariffSaveFirst"];
            RebuildTariff();
            DeleteWholeText = _localizer["ContractDelete"];
            Title = TitleText();
            RebuildLinks();
            RebuildFields();
            OnPropertyChanged(nameof(BillingTimingHint));
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    private static int IndexOf(IReadOnlyList<EnumOption> options, EnumOption? selected) =>
        selected is null ? -1 : options.ToList().IndexOf(selected);

    /// <summary>The option at <paramref name="index"/>; out-of-range values (the picker clearing itself) give null.</summary>
    private static EnumOption? Pick(IReadOnlyList<EnumOption> options, int index) =>
        index >= 0 && index < options.Count ? options[index] : null;

    private static TRow? RowAt<TRow>(Regex pattern, string field, List<TRow> rows)
        where TRow : class
    {
        var match = pattern.Match(field);
        return match.Success && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var index && index < rows.Count
            ? rows[index]
            : null;
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private string TitleText() => IsNew ? _localizer["ContractNew"] : _contract.Title;

    private string FromSupplier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : _localizer.Get("PaymentFromSupplier", value);

    private async Task CopyAsync(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        await _clipboard.SetTextAsync(text);
        _snackbar.Notify(_localizer["Copied"]);
    }

    private Task<bool> ConfirmRowDeleteAsync(string displayText) => Dialogs.ConfirmAsync(
        _localizer["Dialog_DeleteRow_Title"],
        _localizer.Get("Dialog_DeleteRow_Message", displayText),
        _localizer["Delete"],
        _localizer["Cancel"]);

    private PaymentDetails Effective()
    {
        var contract = new Contract { PaymentDetails = BuildPaymentDetails() };
        return PaymentDetails.Effective(contract, SelectedSupplierEntity ?? new Supplier());
    }

    private string RenderPurpose(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var contract = new Contract { BillingTiming = SelectedBillingTiming?.As<BillingTiming>() ?? BillingTiming.InArrears };
        var context = new PaymentPurposeContext(
            NullIfBlank(CustomerNumber),
            NullIfBlank(ContractNumber),
            null,
            Bill.DefaultPeriod(contract, _clock.Today),
            null,
            _propertyName);
        return PaymentPurpose.Render(template, context);
    }

    private void RebuildOptions()
    {
        Services = EnumText.Options<ServiceKind>(_localizer);
        BillingModes = EnumText.Options<BillingMode>(_localizer);
        BillingTimings = EnumText.Options<BillingTiming>(_localizer);
        Currencies = _hasLegacyCurrency
            ? EnumText.Options<Currency>(_localizer)
            : [.. EnumText.Options<Currency>(_localizer).Where(o => o.Value.Equals(Currency.EUR))];
        OnPropertyChanged(nameof(ShowCurrencyPicker));
    }

    /// <summary>Active categories by sort order, plus the selected one if it is archived.</summary>
    private IReadOnlyList<ChoiceOption> CategoryOptions() =>
    [
        .. _categories
            .Where(c => !c.IsArchived || c.Id == SelectedCategory?.Id)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChoiceOption(c.Id, CategoryNames.DisplayName(c, _localizer), c.Color)),
    ];

    private IReadOnlyList<ChoiceOption> SupplierOptions() =>
    [
        .. _suppliers
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ChoiceOption(s.Id, s.Name, string.Empty)),
    ];

    private ChoiceOption? FindCategoryOption(Guid id) =>
        _categories.FirstOrDefault(c => c.Id == id) is { } category
            ? new ChoiceOption(category.Id, CategoryNames.DisplayName(category, _localizer), category.Color)
            : null;

    private ChoiceOption? FindSupplierOption(Guid id) =>
        _suppliers.FirstOrDefault(s => s.Id == id) is { } supplier ? new ChoiceOption(supplier.Id, supplier.Name, string.Empty) : null;

    /// <summary>Selects the built-in category of the chosen service kind; no change if there is none.</summary>
    private void PreselectCategory()
    {
        var kind = SelectedService?.As<ServiceKind>() ?? ServiceKind.ColdWater;
        if (BuiltInCategories.FindFor(kind, _categories) is { } id)
        {
            SelectedCategory = FindCategoryOption(id);
        }
    }

    private LinkRowViewModel CreateLinkRow(Link link)
    {
        var row = new LinkRowViewModel
        {
            Id = link.Id,
            CreatedAt = link.CreatedAt,
            Kind = link.Kind,
            Title = link.Title,
            Url = link.Url,
        };
        row.EditCommand = new RelayCommand(() => EditLinkCommand.Execute(row));
        row.RemoveCommand = new RelayCommand(() => RemoveLinkCommand.Execute(row));
        row.OpenCommand = new RelayCommand(() => OpenLinkCommand.Execute(row));
        return row;
    }

    /// <summary>Rebuilds the own link rows and the muted supplier links the contract does not override.</summary>
    private void RebuildLinks()
    {
        foreach (var row in _linkRows)
        {
            row.DisplayText = string.IsNullOrWhiteSpace(row.Title) ? EnumText.Get(_localizer, row.Kind) : row.Title;
            row.DeleteText = _localizer["Delete"];
            row.OpenText = _localizer["LinkOpen"];
        }

        Links = [.. _linkRows];

        if (SelectedSupplierEntity is { } supplier)
        {
            var own = new Contract { Links = [.. _linkRows.Select((row, i) => new Link { Kind = row.Kind, Url = row.Url, SortOrder = i })] };
            InheritedLinks =
            [
                .. own.GetEffectiveLinks(supplier).Skip(own.Links.Count)
                    .Select(link => new InheritedLinkRowViewModel(link, _localizer, OpenInheritedAsync)),
            ];
        }
        else
        {
            InheritedLinks = [];
        }
    }

    private async Task OpenInheritedAsync(Link link)
    {
        if (link.TryGetUri() is { } uri)
        {
            await _launcher.OpenAsync(uri);
        }
    }

    private CustomFieldRowViewModel CreateFieldRow(CustomFieldDefinition field)
    {
        var row = new CustomFieldRowViewModel
        {
            Id = field.Id,
            CreatedAt = field.CreatedAt,
            Name = field.Name,
            Kind = field.Kind,
            Target = field.Target,
        };
        row.EditCommand = new RelayCommand(() => EditCustomFieldCommand.Execute(row));
        row.RemoveCommand = new RelayCommand(() => RemoveCustomFieldCommand.Execute(row));
        return row;
    }

    private void RebuildFields()
    {
        foreach (var row in _fieldRows)
        {
            row.DisplayText = row.Name;
            row.DetailText = _localizer.Get("CustomFieldRowDetail", EnumText.Get(_localizer, row.Kind), EnumText.Get(_localizer, row.Target));
            row.DeleteText = _localizer["Delete"];
        }

        CustomFields = [.. _fieldRows];
    }

    private bool IsDuplicateField(CustomFieldDefinition candidate, CustomFieldRowViewModel? editing) =>
        _fieldRows.Any(r => !ReferenceEquals(r, editing)
            && r.Target == candidate.Target
            && string.Equals(r.Name.Trim(), candidate.Name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>All fields blank means "no payment details" (null); the IBAN is stored without spaces.</summary>
    private PaymentDetails? BuildPaymentDetails()
    {
        var details = new PaymentDetails(
            NullIfBlank(RecipientName),
            NullIfBlank(RecipientRegistrationNumber),
            NullIfBlank(Iban.Normalize(IbanText)),
            NullIfBlank(Iban.Normalize(Bic)),
            NullIfBlank(BankName),
            NullIfBlank(PurposeTemplate));
        return details == PaymentDetails.Empty ? null : details;
    }

    private int? ParseDay(string text, string field)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            return day;
        }

        _inputErrors = [.. _inputErrors, new ValidationError(CommonErrorCodes.NumberInvalid, field)];
        return null;
    }

    private Contract BuildContract()
    {
        var contract = new Contract
        {
            Id = _contract.Id,
            CreatedAt = _contract.CreatedAt,
            UpdatedAt = _contract.UpdatedAt,
            PropertyId = _propertyId,
            SupplierId = SelectedSupplier?.Id ?? Guid.Empty,
            CategoryId = SelectedCategory?.Id ?? Guid.Empty,
            Service = SelectedService?.As<ServiceKind>() ?? ServiceKind.ColdWater,
            Title = ContractTitle.Trim(),
            CustomerNumber = NullIfBlank(CustomerNumber),
            ContractNumber = NullIfBlank(ContractNumber),
            PortalLogin = NullIfBlank(PortalLogin),
            ValidFrom = ValidFrom,
            ValidTo = HasEndDate ? ValidTo : null,
            Currency = SelectedCurrency?.As<Currency>() ?? Currency.EUR,
            BillingMode = SelectedBillingMode?.As<BillingMode>() ?? BillingMode.Actual,
            BillingTiming = SelectedBillingTiming?.As<BillingTiming>() ?? BillingTiming.InArrears,
            VatCalculation = VatOnTotal ? VatCalculation.OnTotal : VatCalculation.PerLine,
            RemindAboutReadings = RemindAboutReadings,
            ReadingDayFrom = HasReadingWindow ? ParseDay(ReadingDayFromText, nameof(Contract.ReadingDayFrom)) : null,
            ReadingDayTo = HasReadingWindow ? ParseDay(ReadingDayToText, nameof(Contract.ReadingDayTo)) : null,
            BillDay = ParseDay(BillDayText, nameof(Contract.BillDay)),
            PaymentDueDay = ParseDay(PaymentDueDayText, nameof(Contract.PaymentDueDay)),
            PaymentDetails = BuildPaymentDetails(),
            Notes = NullIfBlank(Notes),
            IsArchived = IsArchived,
        };

        for (var i = 0; i < _linkRows.Count; i++)
        {
            var row = _linkRows[i];
            contract.Links.Add(new Link
            {
                Id = row.Id,
                CreatedAt = row.CreatedAt,
                OwnerId = contract.Id,
                Kind = row.Kind,
                Title = NullIfBlank(row.Title ?? string.Empty),
                Url = row.Url.Trim(),
                SortOrder = i,
            });
        }

        for (var i = 0; i < _fieldRows.Count; i++)
        {
            var row = _fieldRows[i];
            contract.CustomFields.Add(new CustomFieldDefinition
            {
                Id = row.Id,
                CreatedAt = row.CreatedAt,
                OwnerId = contract.Id,
                Name = row.Name.Trim(),
                Kind = row.Kind,
                Target = row.Target,
                SortOrder = i,
            });
        }

        return contract;
    }
}
