using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Editor of one bill (a supplier bill of a contract, or a one-off expense). Shown in the side pane of the Bills screen or
/// as a page (<see cref="Routes.BillEdit"/>). The cards: General, Amounts, Comparison with the calculation, Lines,
/// Custom fields, Document, Payment, Notes. The payment card does the "Paid" quick action and its undo, adds a payment in the
/// payment editor and uses a prepayment of the contract.
/// </summary>
public sealed partial class BillEditViewModel : PaneEditorViewModel
{
    protected override HelpTopic? GuideTopic => HelpTopic.Bills;

    /// <summary>Navigation parameter (<c>yyyy-MM</c>) with the period a new bill starts with.</summary>
    public const string PeriodParameter = "period";

    /// <summary>Navigation parameter (a <see cref="decimal"/>) with the calculated total shown as a hint in the Total field.</summary>
    public const string GrossParameter = "gross";

    /// <summary>The properties that mark the editor dirty when the user changes them.</summary>
    private static readonly HashSet<string> DirtyFields =
    [
        nameof(IsOneOff), nameof(SelectedProperty), nameof(SelectedContract), nameof(Period), nameof(SelectedKind),
        nameof(BillTitle), nameof(Number), nameof(HasIssuedOn), nameof(IssuedOn), nameof(HasDueOn), nameof(DueOn),
        nameof(HasServicePeriod), nameof(ServiceFrom), nameof(ServiceTo), nameof(SelectedCategory), nameof(SelectedSupplier),
        nameof(ConsumptionText), nameof(Notes),
    ];

    private static readonly BillKind[] ContractKinds =
        [BillKind.Regular, BillKind.Equalized, BillKind.Settlement, BillKind.Correction, BillKind.TaxInstallment];

    private readonly IBillStore _bills;
    private readonly IPaymentStore _paymentStore;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IPropertyStore _propertyStore;
    private readonly IChargeInputLoader _chargeLoader;
    private readonly IAttachmentStore _attachmentStore;
    private readonly IMediaService _media;
    private readonly IFileViewer _viewer;
    private readonly QuickPayService _quickPay;
    private readonly PrepaymentService _prepayments;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _snackbar;
    private readonly IErrorLog _errorLog;

    private Bill _bill = new();
    private Bill? _pending;
    private IReadOnlyList<Property> _properties = [];
    private IReadOnlyList<Contract> _contracts = [];
    private IReadOnlyList<Supplier> _suppliers = [];
    private IReadOnlyList<Category> _categories = [];
    private Contract? _contract;
    private ChargeInput? _input;
    private CalculatedCharge? _live;
    private CalculatedCharge? _snapshot;
    private bool _loading;
    private bool _applying;
    private bool _periodTouched;
    private bool _dueTouched;
    private bool _categoryTouched;
    private bool _supplierTouched;
    private int _calculationVersion;
    private List<int> _rowOfValue = [];

    [ObservableProperty]
    private BillEditTexts _texts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTypeIndex), nameof(ShowContractRow), nameof(IsKindEditable), nameof(ShowComparison), nameof(TitleRequiredMark))]
    private bool _isOneOff;

    [ObservableProperty]
    private IReadOnlyList<ChoiceOption> _typeChoices = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyName), nameof(ShowPropertyChooser))]
    private ChoiceOption? _selectedProperty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedContractName))]
    private ChoiceOption? _selectedContract;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodText), nameof(PickerDate))]
    private YearMonth _period;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    private string _billTitle = string.Empty;

    [ObservableProperty]
    private string _number = string.Empty;

    [ObservableProperty]
    private bool _hasIssuedOn;

    [ObservableProperty]
    private DateOnly _issuedOn;

    [ObservableProperty]
    private bool _hasDueOn;

    [ObservableProperty]
    private DateOnly _dueOn;

    [ObservableProperty]
    private bool _hasServicePeriod;

    [ObservableProperty]
    private DateOnly _serviceFrom;

    [ObservableProperty]
    private DateOnly _serviceTo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryName), nameof(SelectedCategoryColor), nameof(HasCategoryColor))]
    private ChoiceOption? _selectedCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSupplierName))]
    private ChoiceOption? _selectedSupplier;

    [ObservableProperty]
    private string _currencySymbol = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    // Quantities from the bill card.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQuantities))]
    private IReadOnlyList<BillQuantityRowViewModel> _quantityRows = [];

    // Comparison card.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowComparison))]
    private bool _hasTariff;

    [ObservableProperty]
    private string _calculatedText = string.Empty;

    [ObservableProperty]
    private string _differenceText = string.Empty;

    [ObservableProperty]
    private bool _differenceHigher;

    [ObservableProperty]
    private bool _differenceLower;

    [ObservableProperty]
    private string _consumptionText = string.Empty;

    [ObservableProperty]
    private string _meteredText = string.Empty;

    [ObservableProperty]
    private string _quantityDifferenceText = string.Empty;

    [ObservableProperty]
    private string _comparisonNote = string.Empty;

    [ObservableProperty]
    private string _snapshotChangedText = string.Empty;

    // Lines card.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLines), nameof(HasLines))]
    private IReadOnlyList<BillLineRowViewModel> _lineRows = [];

    [ObservableProperty]
    private bool _linesExpanded;

    [ObservableProperty]
    private string _linesWarning = string.Empty;

    // Custom fields card.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFields), nameof(HasNoFields))]
    private IReadOnlyList<CustomFieldValueRowViewModel> _fieldRows = [];

    // Document card.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAttachments))]
    private IReadOnlyList<BillAttachmentRowViewModel> _attachmentRows = [];

    [ObservableProperty]
    private string _attachmentError = string.Empty;

    // Payment card.
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusPaid), nameof(IsStatusPartial), nameof(IsStatusOverdue))]
    private BillBadgeKind _statusKind;

    [ObservableProperty]
    private string _paidAmountText = string.Empty;

    [ObservableProperty]
    private string _openAmountText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPayments))]
    private IReadOnlyList<BillPaymentRowViewModel> _paymentRows = [];

    [ObservableProperty]
    private bool _canMarkPaid;

    [ObservableProperty]
    private bool _canUsePrepayment;

    [ObservableProperty]
    private string _usePrepaymentText = string.Empty;

    public BillEditViewModel(
        IBillStore bills,
        IPaymentStore paymentStore,
        IContractStore contractStore,
        ISupplierStore supplierStore,
        ICategoryStore categoryStore,
        IPropertyStore propertyStore,
        IChargeInputLoader chargeLoader,
        IAttachmentStore attachmentStore,
        IMediaService media,
        IFileViewer viewer,
        QuickPayService quickPay,
        PrepaymentService prepayments,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar,
        IErrorLog errorLog)
        : base(localizer, dialogs, navigation, snackbar)
    {
        _bills = bills;
        _paymentStore = paymentStore;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _categoryStore = categoryStore;
        _propertyStore = propertyStore;
        _chargeLoader = chargeLoader;
        _attachmentStore = attachmentStore;
        _media = media;
        _viewer = viewer;
        _quickPay = quickPay;
        _prepayments = prepayments;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _snackbar = snackbar;
        _errorLog = errorLog;
        _texts = new BillEditTexts(localizer);
        _period = YearMonth.From(clock.Today);
        _issuedOn = clock.Today;
        _dueOn = clock.Today;
        _serviceFrom = clock.Today;
        _serviceTo = clock.Today;
        Amounts.Edited += OnAmountsEdited;
        PropertyChanged += OnOwnPropertyChanged;
        RefreshTexts();
    }

    public override Guid EntityId => _bill.Id;

    /// <summary>The amount fields (Net / VAT % / VAT / Total).</summary>
    public AmountEditor Amounts { get; } = new();

    // Presets of a new bill (from the charges screen or a contract); set before LoadAsync(null).

    /// <summary>The property a new bill starts with.</summary>
    public Guid? PresetPropertyId { get; set; }

    /// <summary>The contract a new bill starts with.</summary>
    public Guid? PresetContractId { get; set; }

    /// <summary>The period a new bill starts with.</summary>
    public YearMonth? PresetPeriod { get; set; }

    /// <summary>The calculated total shown as a hint in the empty Total field.</summary>
    public decimal? PresetGross { get; set; }

    /// <summary>The last calculation started after a contract or period change; tests await it.</summary>
    public Task PendingRefresh { get; private set; } = Task.CompletedTask;

    // Visibility.

    public bool ShowTypePicker => IsNew;

    public bool ShowContractRow => !IsOneOff;

    public bool IsKindEditable => !IsOneOff;

    public bool ShowComparison => !IsOneOff && HasTariff;

    public bool ShowQuantities => !IsOneOff && QuantityRows.Count > 0;

    public bool ShowPaymentCard => !IsNew;

    public bool ShowPropertyChooser => IsNew && _properties.Count > 1;

    public bool IsPropertyReadOnly => !ShowPropertyChooser;

    public bool IsContractEditable => IsNew;

    public bool IsContractReadOnly => !IsNew;

    public string TitleRequiredMark => IsOneOff ? " *" : string.Empty;

    public bool HasLines => LineRows.Count > 0;

    public bool HasNoLines => LineRows.Count == 0;

    public bool HasFields => FieldRows.Count > 0;

    public bool HasNoFields => FieldRows.Count == 0;

    public bool HasNoAttachments => AttachmentRows.Count == 0;

    public bool HasNoPayments => PaymentRows.Count == 0;

    public bool ShowDifference => DifferenceText.Length > 0;

    public bool ShowQuantityDifference => QuantityDifferenceText.Length > 0;

    public bool ShowSnapshotChanged => SnapshotChangedText.Length > 0;

    public bool ShowComparisonNote => ComparisonNote.Length > 0;

    public bool HasLinesWarning => LinesWarning.Length > 0;

    public bool IsStatusPaid => StatusKind is BillBadgeKind.Paid or BillBadgeKind.Prepaid;

    public bool IsStatusPartial => StatusKind == BillBadgeKind.PartiallyPaid;

    public bool IsStatusOverdue => StatusKind == BillBadgeKind.Overdue;

    public bool HasCategoryColor => SelectedCategoryColor.Length > 0;

    // Derived texts.

    public int SelectedTypeIndex
    {
        get => IsOneOff ? 1 : 0;
        set
        {
            if (IsNew && value is 0 or 1)
            {
                IsOneOff = value == 1;
            }
        }
    }

    public int SelectedKindIndex
    {
        get => SelectedKind is null ? -1 : Kinds.ToList().IndexOf(SelectedKind);
        set
        {
            if (value >= 0 && value < Kinds.Count)
            {
                SelectedKind = Kinds[value];
            }
        }
    }

    public string SelectedPropertyName => SelectedProperty?.Name ?? string.Empty;

    public string SelectedContractName => SelectedContract?.Name ?? _localizer["BillContractNone"];

    public string SelectedCategoryName => SelectedCategory?.Name ?? string.Empty;

    public string SelectedCategoryColor => SelectedCategory?.Color ?? string.Empty;

    public string SelectedSupplierName => SelectedSupplier?.Name ?? _localizer["BillNoSupplier"];

    /// <summary>"October 2026".</summary>
    public string PeriodText
    {
        get
        {
            var text = Period.FirstDay.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            return text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
        }
    }

    /// <summary>The period as a date for the platform date picker; dates outside the supported range are clamped.</summary>
    public DateOnly PickerDate
    {
        get => Period.FirstDay;
        set
        {
            var earliest = ChargesViewModel.EarliestMonth;
            var latest = ChargesViewModel.LatestMonth;
            Period = value.Year < earliest.Year ? earliest : value.Year > latest.Year ? latest : YearMonth.From(value);
        }
    }

    // Error texts of the fields.

    public string PropertyError => ErrorOf(nameof(Bill.PropertyId));

    public string ContractError => ErrorOf(nameof(Bill.ContractId));

    public string TitleError => ErrorOf(nameof(Bill.Title));

    public string NumberError => ErrorOf(nameof(Bill.Number));

    public string DueOnError => ErrorOf(nameof(Bill.DueOn));

    public string ServiceFromError => ErrorOf(nameof(Bill.ServiceFrom));

    public string ServiceToError => ErrorOf(nameof(Bill.ServiceTo));

    public string CategoryError => ErrorOf(nameof(Bill.CategoryId));

    public string NetError => ErrorOf("Net");

    public string VatPercentError => ErrorOf("VatPercent");

    public string GrossError => ErrorOf("Gross");

    public string ConsumptionError => ErrorOf(nameof(Bill.ConsumptionOnBill));

    public string NotesError => ErrorOf(nameof(Bill.Notes));

    public string LinesError => string.Join("\n", Errors.Where(e => e.Key.StartsWith("Lines[", StringComparison.Ordinal)).Select(e => e.Value).Distinct());

    public bool HasLinesError => LinesError.Length > 0;

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        _loading = true;
        try
        {
            var existing = id is { } value ? await _bills.GetAsync(value) : null;
            _bill = existing ?? new Bill { Kind = BillKind.Regular, IssuedOn = _clock.Today };
            _pending = null;
            IsNew = existing is null;

            _properties = await _propertyStore.ListAsync(false);
            if (existing is not null && _properties.All(p => p.Id != existing.PropertyId) && await _propertyStore.GetAsync(existing.PropertyId) is { } own)
            {
                _properties = [.. _properties, own];
            }

            _suppliers = await _supplierStore.ListAsync();
            _categories = await _categoryStore.ListAsync();
            _categoryTouched = !IsNew;
            _supplierTouched = !IsNew;
            _dueTouched = !IsNew;
            _periodTouched = !IsNew;

            if (existing is null)
            {
                await PrepareNewAsync();
            }
            else
            {
                await ShowExistingAsync(existing);
            }

            ClearErrors();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            _loading = false;
            SuppressDirty = previous;
        }

        await RefreshCalculationAsync();
        await RefreshPaymentsAsync();
        UpdateComparison();

        // A page binds before the data arrives: have every binding read the loaded state.
        OnPropertyChanged(string.Empty);
    }

    protected override IReadOnlyList<ValidationError> Validate()
    {
        var errors = new List<ValidationError>();
        var bill = BuildBill(errors);
        var contract = IsOneOff ? null : _contract;
        errors.AddRange(BillValidator.Validate(bill, contract).Errors.Select(MapRowIndex));
        _pending = bill;
        return errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var bill = _pending ?? throw new InvalidOperationException("Validate must run before saving.");
        var attachmentIds = await StoreAttachmentsAsync(bill);
        bill.AttachmentIds = attachmentIds;
        await _bills.SaveAsync(bill);

        foreach (var removed in _removedAttachments)
        {
            await _attachmentStore.DeleteAsync(removed);
        }

        _removedAttachments.Clear();
        _pendingFiles.Clear();
        _bill = bill;
        _snapshot = bill.CalculatedSnapshot;
        IsNew = false;
        Title = TitleText();
        await RefreshAttachmentRowsAsync();
        RebuildLines();
        await RefreshPaymentsAsync();
        UpdateComparison();
    }

    protected override async Task DeleteCoreAsync()
    {
        await _quickPay.ReleasePaymentsAsync(_bill.Id);
        await _bills.DeleteAsync(_bill.Id);
    }

    protected override string DeleteName() => TitleText();

    protected override string DeleteMessage() =>
        _paymentList.Count > 0 ? _localizer["BillDeleteWithPayments"] : base.DeleteMessage();

    protected override void OnErrorsChanged()
    {
        foreach (var name in new[]
        {
            nameof(PropertyError), nameof(ContractError), nameof(TitleError), nameof(NumberError), nameof(DueOnError),
            nameof(ServiceFromError), nameof(ServiceToError), nameof(CategoryError), nameof(NetError), nameof(VatPercentError),
            nameof(GrossError), nameof(ConsumptionError), nameof(NotesError), nameof(LinesError), nameof(HasLinesError),
        })
        {
            OnPropertyChanged(name);
        }

        for (var i = 0; i < FieldRows.Count; i++)
        {
            FieldRows[i].ErrorText = Errors.GetValueOrDefault($"CustomFields[{i}].Value", string.Empty);
        }

        for (var i = 0; i < QuantityRows.Count; i++)
        {
            QuantityRows[i].ErrorText = Errors.GetValueOrDefault(QuantityField(i), string.Empty);
        }
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Texts = new BillEditTexts(_localizer);
        DeleteWholeText = Texts.DeleteBillText;
        TypeChoices =
        [
            new ChoiceOption(Guid.Empty, _localizer["BillTypeContract"], string.Empty),
            new ChoiceOption(Guid.Empty, _localizer["BillTypeOneOff"], string.Empty),
        ];
        OnPropertyChanged(nameof(SelectedTypeIndex));
        RebuildKinds();
        OnPropertyChanged(nameof(SelectedContractName));
        OnPropertyChanged(nameof(SelectedSupplierName));
        OnPropertyChanged(nameof(PeriodText));
        foreach (var row in FieldRows)
        {
            row.DateSetText = _localizer["BillFieldDateSet"];
        }

        if (_bill.Id != Guid.Empty)
        {
            Title = TitleText();
        }

        UpdateComparison();
        RebuildLines();
        UpdatePaymentCard();
    }

    private string ErrorOf(string field) => Errors.GetValueOrDefault(field, string.Empty);

    private void OnOwnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not { } name)
        {
            return;
        }

        if (name == nameof(DifferenceText))
        {
            OnPropertyChanged(nameof(ShowDifference));
        }
        else if (name == nameof(QuantityDifferenceText))
        {
            OnPropertyChanged(nameof(ShowQuantityDifference));
        }
        else if (name == nameof(SnapshotChangedText))
        {
            OnPropertyChanged(nameof(ShowSnapshotChanged));
        }
        else if (name == nameof(ComparisonNote))
        {
            OnPropertyChanged(nameof(ShowComparisonNote));
        }
        else if (name == nameof(LinesWarning))
        {
            OnPropertyChanged(nameof(HasLinesWarning));
        }
        else if (name == nameof(IsNew))
        {
            foreach (var derived in new[]
            {
                nameof(ShowTypePicker), nameof(ShowPaymentCard), nameof(ShowPropertyChooser), nameof(IsPropertyReadOnly),
                nameof(IsContractEditable), nameof(IsContractReadOnly),
            })
            {
                OnPropertyChanged(derived);
            }
        }

        if (!DirtyFields.Contains(name))
        {
            return;
        }

        if (!_applying && !_loading)
        {
            switch (name)
            {
                case nameof(HasDueOn) or nameof(DueOn):
                    _dueTouched = true;
                    break;
                case nameof(SelectedCategory):
                    _categoryTouched = true;
                    break;
                case nameof(SelectedSupplier):
                    _supplierTouched = true;
                    break;
            }
        }

        MarkDirty();
    }

    private void OnAmountsEdited()
    {
        MarkDirty();
        UpdateComparison();
    }

    partial void OnConsumptionTextChanged(string value) => UpdateComparison();

    partial void OnIsOneOffChanged(bool value)
    {
        if (_loading || !IsNew)
        {
            return;
        }

        // A different kind of bill starts over: the contract and everything taken from it go. A one-off expense belongs to
        // the current month, a contract bill to the previous one until the contract says otherwise.
        if (!_periodTouched)
        {
            _applying = true;
            try
            {
                Period = YearMonth.From(_clock.Today).AddMonths(value ? 0 : -1);
            }
            finally
            {
                _applying = false;
            }
        }

        _contract = null;
        SelectedContract = null;
        _live = null;
        _snapshot = null;
        HasTariff = false;
        RebuildKinds();
        SelectedKind = value
            ? Kinds.First(k => k.Value.Equals(BillKind.OneOff))
            : Kinds.First(k => k.Value.Equals(BillKind.Regular));
        UpdateComparison();
    }

    partial void OnHasServicePeriodChanged(bool value)
    {
        if (value && !_loading && !_applying)
        {
            // The usual case: one bill for the months around the period; start from the period itself.
            ServiceFrom = Period.FirstDay;
            ServiceTo = Period.LastDay;
        }
    }

    partial void OnPeriodChanged(YearMonth value)
    {
        if (_loading || _applying)
        {
            return;
        }

        _periodTouched = true;
        if (_snapshot is { } snapshot && snapshot.Period != value)
        {
            _snapshot = null;
        }

        if (!_dueTouched)
        {
            ApplyDefaultDue();
        }

        PendingRefresh = RefreshCalculationAsync();
    }

    private string TitleText()
    {
        if (IsNew)
        {
            return _localizer["BillNewTitle"];
        }

        if (IsOneOff || _bill.Kind == BillKind.OneOff)
        {
            return _localizer.Get("BillOneOffTitle", string.IsNullOrWhiteSpace(BillTitle) ? _bill.Title ?? string.Empty : BillTitle.Trim());
        }

        var name = _contract?.Title ?? string.Empty;
        return _localizer.Get("BillEditTitle", $"{name} {BillText.Period(_bill.Period)}".Trim());
    }

    private void RebuildKinds()
    {
        var current = SelectedKind?.Value;
        Kinds = [.. (IsOneOff ? new[] { BillKind.OneOff } : ContractKinds).Select(k => new EnumOption(k, EnumText.Get(_localizer, k)))];
        SelectedKind = current is not null
            ? Kinds.FirstOrDefault(k => k.Value.Equals(current)) ?? Kinds[0]
            : Kinds[0];
    }

    /// <summary>Reads the form into a bill; unreadable numbers are added to <paramref name="errors"/>.</summary>
    private Bill BuildBill(List<ValidationError> errors)
    {
        var amountErrors = Amounts.TryRead(out var amounts);
        errors.AddRange(amountErrors);
        var consumption = AmountEditor.ReadOptional(ConsumptionText, nameof(Bill.ConsumptionOnBill), errors);
        var quantities = new List<BillQuantity>();
        for (var i = 0; i < QuantityRows.Count; i++)
        {
            if (AmountEditor.ReadOptional(QuantityRows[i].Text, QuantityField(i), errors) is { } typed)
            {
                quantities.Add(new BillQuantity(QuantityRows[i].ComponentId, typed));
            }
        }

        var values = CustomFieldValueRowViewModel.Read(FieldRows, errors, "CustomFields", out var rowOfValue);
        _rowOfValue = rowOfValue;

        var bill = new Bill
        {
            Id = _bill.Id,
            CreatedAt = _bill.CreatedAt,
            UpdatedAt = _bill.UpdatedAt,
            TemplateId = _bill.TemplateId,
            PropertyId = SelectedProperty?.Id ?? Guid.Empty,
            CategoryId = SelectedCategory?.Id ?? Guid.Empty,
            ContractId = IsOneOff ? null : _contract?.Id,
            SupplierId = SelectedSupplier is { } supplier && supplier.Id != Guid.Empty ? supplier.Id : null,
            Period = Period,
            ServiceFrom = HasServicePeriod ? ServiceFrom : null,
            ServiceTo = HasServicePeriod ? ServiceTo : null,
            Kind = IsOneOff ? BillKind.OneOff : SelectedKind?.As<BillKind>() ?? BillKind.Regular,
            Title = string.IsNullOrWhiteSpace(BillTitle) ? null : BillTitle.Trim(),
            Number = string.IsNullOrWhiteSpace(Number) ? null : Number.Trim(),
            IssuedOn = HasIssuedOn ? IssuedOn : null,
            DueOn = HasDueOn ? DueOn : null,
            Net = amounts?.Net,
            VatPercent = amounts?.VatPercent,
            Vat = amounts?.Vat,
            Gross = amounts?.Gross ?? 0m,
            Currency = _bill.Currency,
            ConsumptionOnBill = consumption,
            BillQuantities = IsOneOff ? [] : quantities,
            CustomFields = values,
            CalculatedSnapshot = IsOneOff ? null : _snapshot ?? (HasTariff ? _live : null),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        };
        bill.Lines = [.. _lines.Select((line, i) =>
        {
            line.BillId = bill.Id;
            line.SortOrder = i;
            return line;
        })];
        bill.AttachmentIds = [.. _bill.AttachmentIds.Where(id => !_removedAttachments.Contains(id))];
        return bill;
    }

    /// <summary>The validator indexes custom field errors by value; the form shows them on the row the value came from.</summary>
    private ValidationError MapRowIndex(ValidationError error)
    {
        var index = CustomFieldValueRowViewModel.RowIndexOf(error.Field);
        return index is { } n && n < _rowOfValue.Count
            ? error with { Field = $"CustomFields[{_rowOfValue[n]}].Value" }
            : error;
    }

    private void ApplyDefaultDue()
    {
        if (_contract is null)
        {
            return;
        }

        _applying = true;
        try
        {
            var due = BillDefaults.DueDate(_contract, Period);
            HasDueOn = due is not null;
            if (due is { } date)
            {
                DueOn = date;
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private string Money(decimal amount) => BillText.Money(amount, CurrencySymbol);
}
