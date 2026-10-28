using System.Globalization;
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
/// Editor of one bill template: how a recurring or repeated payment looks (property, category, contract, supplier, title,
/// amounts, lines, custom fields) and when it comes (schedule, timing, due date), with a preview of the next drafts.
/// Shown in the side pane of the Bill templates screen or as a page (<see cref="Routes.BillTemplateEdit"/>).
/// </summary>
public sealed partial class BillTemplateEditViewModel : PaneEditorViewModel
{
    protected override HelpTopic? GuideTopic => HelpTopic.BillTemplates;

    /// <summary>The properties that mark the editor dirty when the user changes them.</summary>
    private static readonly HashSet<string> DirtyFields =
    [
        nameof(TemplateName), nameof(BillTitle), nameof(Notes), nameof(IsActive), nameof(SelectedProperty), nameof(SelectedCategory),
        nameof(SelectedContract), nameof(SelectedSupplier), nameof(SelectedKind), nameof(SelectedTiming), nameof(EveryMonthsText),
        nameof(StartMonth), nameof(HasEnd), nameof(EndMonth), nameof(DayText), nameof(DueOffsetText), nameof(RemindText),
    ];

    /// <summary>The properties after which the preview of the next drafts is rebuilt.</summary>
    private static readonly HashSet<string> PreviewFields =
    [
        nameof(SelectedKind), nameof(SelectedTiming), nameof(EveryMonthsText), nameof(StartMonth), nameof(HasEnd), nameof(EndMonth),
        nameof(DayText), nameof(DueOffsetText),
    ];

    private readonly IBillTemplateStore _templates;
    private readonly IBillStore _billStore;
    private readonly IPropertyStore _propertyStore;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly ICategoryStore _categoryStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;

    private readonly List<BillLine> _lines = [];
    private readonly List<CustomFieldRowViewModel> _fieldRows = [];
    private BillTemplate _template = new();
    private BillTemplate? _pending;
    private IReadOnlyList<Property> _properties = [];
    private IReadOnlyList<Contract> _contracts = [];
    private IReadOnlyList<Supplier> _suppliers = [];
    private IReadOnlyList<Category> _categories = [];
    private Contract? _contract;
    private bool _loading;
    private bool _categoryTouched;
    private bool _supplierTouched;

    [ObservableProperty]
    private BillTemplateEditTexts _texts;

    [ObservableProperty]
    private string _templateName = string.Empty;

    [ObservableProperty]
    private string _billTitle = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyName), nameof(ShowPropertyChooser))]
    private ChoiceOption? _selectedProperty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryName), nameof(SelectedCategoryColor), nameof(HasCategoryColor))]
    private ChoiceOption? _selectedCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedContractName))]
    private ChoiceOption? _selectedContract;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSupplierName))]
    private ChoiceOption? _selectedSupplier;

    [ObservableProperty]
    private string _currencySymbol = string.Empty;

    // Schedule.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex), nameof(ShowPeriodFields), nameof(ShowEveryMonths))]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    private string _everyMonthsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartDate))]
    private YearMonth _startMonth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEndPicker))]
    private bool _hasEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndDate))]
    private YearMonth _endMonth;

    [ObservableProperty]
    private string _dayText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTimingIndex))]
    private IReadOnlyList<EnumOption> _timings = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTimingIndex))]
    private EnumOption? _selectedTiming;

    [ObservableProperty]
    private string _dueOffsetText = string.Empty;

    [ObservableProperty]
    private string _remindText = string.Empty;

    [ObservableProperty]
    private string _previewText = string.Empty;

    // Lines and custom fields.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLines), nameof(HasLines))]
    private IReadOnlyList<BillLineRowViewModel> _lineRows = [];

    [ObservableProperty]
    private bool _linesExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCustomFields), nameof(HasCustomFields))]
    private IReadOnlyList<CustomFieldRowViewModel> _customFields = [];

    public BillTemplateEditViewModel(
        IBillTemplateStore templates,
        IBillStore billStore,
        IPropertyStore propertyStore,
        IContractStore contractStore,
        ISupplierStore supplierStore,
        ICategoryStore categoryStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar)
        : base(localizer, dialogs, navigation, snackbar)
    {
        _templates = templates;
        _billStore = billStore;
        _propertyStore = propertyStore;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _categoryStore = categoryStore;
        _clock = clock;
        _localizer = localizer;
        _texts = new BillTemplateEditTexts(localizer);
        _startMonth = YearMonth.From(clock.Today);
        _endMonth = YearMonth.From(clock.Today);
        Amounts.Edited += OnAmountsEdited;
        PropertyChanged += OnOwnPropertyChanged;
        SuppressDirty = true;
        RefreshTexts();
        SuppressDirty = false;
    }

    public override Guid EntityId => _template.Id;

    /// <summary>The default amount (Net / VAT % / VAT / Total); may stay empty.</summary>
    public AmountEditor Amounts { get; } = new();

    // Presets of a new template; set before LoadAsync(null).

    /// <summary>The contract a new template starts from ("Template from this contract").</summary>
    public Guid? PresetContractId { get; set; }

    /// <summary>The bill a new template starts from ("Save as template").</summary>
    public Guid? PresetBillId { get; set; }

    // Visibility and derived texts.

    public bool ShowPropertyChooser => _properties.Count > 1;

    public bool ShowPeriodFields => SelectedKind?.Value is not ScheduleKind.Once;

    public bool ShowEveryMonths => SelectedKind?.Value is ScheduleKind.EveryNMonths;

    public bool ShowEndPicker => HasEnd;

    public bool HasCategoryColor => SelectedCategoryColor.Length > 0;

    public bool HasLines => LineRows.Count > 0;

    public bool HasNoLines => LineRows.Count == 0;

    public bool HasCustomFields => CustomFields.Count > 0;

    public bool HasNoCustomFields => CustomFields.Count == 0;

    public bool HasPreview => PreviewText.Length > 0;

    public string SelectedPropertyName => SelectedProperty?.Name ?? string.Empty;

    public string SelectedCategoryName => SelectedCategory?.Name ?? string.Empty;

    public string SelectedCategoryColor => SelectedCategory?.Color ?? string.Empty;

    public string SelectedContractName => SelectedContract?.Name ?? _localizer["BillContractNone"];

    public string SelectedSupplierName => SelectedSupplier?.Name ?? _localizer["BillNoSupplier"];

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

    public int SelectedTimingIndex
    {
        get => SelectedTiming is null ? -1 : Timings.ToList().IndexOf(SelectedTiming);
        set
        {
            if (value >= 0 && value < Timings.Count)
            {
                SelectedTiming = Timings[value];
            }
        }
    }

    /// <summary>The first period as a date for the platform date picker (only month and year count).</summary>
    public DateOnly StartDate
    {
        get => StartMonth.FirstDay;
        set => StartMonth = Clamp(value);
    }

    /// <summary>The last period as a date for the platform date picker.</summary>
    public DateOnly EndDate
    {
        get => EndMonth.FirstDay;
        set => EndMonth = Clamp(value);
    }

    // Error texts of the fields.

    public string NameError => ErrorOf(nameof(BillTemplate.Name));

    public string PropertyError => ErrorOf(nameof(BillTemplate.PropertyId));

    public string CategoryError => ErrorOf(nameof(BillTemplate.CategoryId));

    public string TitleError => ErrorOf(nameof(BillTemplate.Title));

    public string NetError => ErrorOf("Net");

    public string VatPercentError => ErrorOf("VatPercent");

    public string GrossError => ErrorOf("Gross");

    public string StartError => ErrorOf("Schedule.StartMonth");

    public string EveryMonthsError => ErrorOf("Schedule.EveryMonths");

    public string EndError => ErrorOf("Schedule.EndMonth");

    public string DayError => ErrorOf("Schedule.DayOfMonth");

    public string DueOffsetError => ErrorOf(nameof(BillTemplate.DueDayOffset));

    public string RemindError => ErrorOf(nameof(BillTemplate.RemindDaysBefore));

    public string NotesError => ErrorOf(nameof(BillTemplate.Notes));

    public string LinesError => string.Join("\n", Errors.Where(e => e.Key.StartsWith("Lines[", StringComparison.Ordinal)).Select(e => e.Value).Distinct());

    public bool HasLinesError => LinesError.Length > 0;

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        _loading = true;
        try
        {
            var existing = id is { } value ? await _templates.GetAsync(value) : null;
            _template = existing ?? new BillTemplate();
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

            RebuildChoices();
            if (existing is not null)
            {
                await ShowExistingAsync(existing);
            }
            else
            {
                await PrepareNewAsync();
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

        UpdatePreview();
        OnPropertyChanged(string.Empty);
    }

    protected override IReadOnlyList<ValidationError> Validate()
    {
        var errors = new List<ValidationError>();
        var template = BuildTemplate(errors);
        errors.AddRange(BillTemplateValidator.Validate(template).Errors);
        _pending = template;
        return errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var template = _pending ?? throw new InvalidOperationException("Validate must run before saving.");
        await _templates.SaveAsync(template);
        _template = template;
        IsNew = false;
        Title = TitleText();

        // A template saved with a start month in the past has drafts due right away.
        await _templates.GenerateDraftsAsync(_clock.Today);
        _template = await _templates.GetAsync(template.Id) ?? template;
    }

    protected override Task DeleteCoreAsync() => _templates.DeleteAsync(_template.Id);

    protected override string DeleteName() => string.IsNullOrWhiteSpace(_template.Name) ? TemplateName : _template.Name;

    protected override string DeleteMessage() => _localizer["BillTemplateDeleteMessage"];

    protected override void OnErrorsChanged()
    {
        foreach (var name in new[]
        {
            nameof(NameError), nameof(PropertyError), nameof(CategoryError), nameof(TitleError), nameof(NetError), nameof(VatPercentError),
            nameof(GrossError), nameof(StartError), nameof(EveryMonthsError), nameof(EndError), nameof(DayError), nameof(DueOffsetError),
            nameof(RemindError), nameof(NotesError), nameof(LinesError), nameof(HasLinesError),
        })
        {
            OnPropertyChanged(name);
        }

        for (var i = 0; i < _fieldRows.Count; i++)
        {
            _fieldRows[i].ErrorText = Errors.GetValueOrDefault($"CustomFields[{i}].Name", string.Empty);
        }
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Texts = new BillTemplateEditTexts(_localizer);
        DeleteWholeText = _localizer["BillTemplateDelete"];
        var kind = SelectedKind?.Value;
        var timing = SelectedTiming?.Value;
        Kinds = EnumText.Options<ScheduleKind>(_localizer);
        Timings = EnumText.Options<BillingTiming>(_localizer);
        SelectedKind = Kinds.FirstOrDefault(k => k.Value.Equals(kind ?? ScheduleKind.Monthly)) ?? Kinds[0];
        SelectedTiming = Timings.FirstOrDefault(t => t.Value.Equals(timing ?? BillingTiming.InArrears)) ?? Timings[0];
        CurrencySymbol = _localizer["Currency_Symbol_" + (_contract?.Currency ?? Currency.EUR)];
        OnPropertyChanged(nameof(SelectedContractName));
        OnPropertyChanged(nameof(SelectedSupplierName));
        if (_template.Id != Guid.Empty)
        {
            Title = TitleText();
        }

        RebuildLines();
        RebuildFields();
        UpdatePreview();
    }

    // ---- Loading --------------------------------------------------------------------------------------------------

    private void RebuildChoices()
    {
        Kinds = EnumText.Options<ScheduleKind>(_localizer);
        Timings = EnumText.Options<BillingTiming>(_localizer);
    }

    private async Task PrepareNewAsync()
    {
        _lines.Clear();
        _fieldRows.Clear();
        _contract = null;
        _contracts = [];
        TemplateName = string.Empty;
        BillTitle = string.Empty;
        Notes = string.Empty;
        IsActive = true;
        DayText = string.Empty;
        DueOffsetText = string.Empty;
        RemindText = string.Empty;
        EveryMonthsText = string.Empty;
        HasEnd = false;
        StartMonth = YearMonth.From(_clock.Today);
        EndMonth = StartMonth;
        Amounts.Clear();
        SelectedKind = Kinds.First(k => k.Value.Equals(ScheduleKind.Monthly));
        SelectedTiming = Timings.First(t => t.Value.Equals(BillingTiming.InArrears));
        SelectedContract = null;
        SelectedSupplier = null;
        SelectedCategory = null;
        SelectedProperty = _properties.Count == 1 ? PropertyOption(_properties[0]) : null;
        CurrencySymbol = _localizer["Currency_Symbol_" + Currency.EUR];

        if (PresetBillId is { } billId && await _billStore.GetAsync(billId) is { } bill)
        {
            await PrepareFromBillAsync(bill);
        }
        else if (PresetContractId is { } contractId && await _contractStore.GetAsync(contractId) is { } contract)
        {
            await PrepareFromContractAsync(contract);
        }
        else if (SelectedProperty is { } property)
        {
            await SelectPropertyCoreAsync(property.Id);
        }

        RebuildLines();
        RebuildFields();
    }

    private async Task PrepareFromContractAsync(Contract contract)
    {
        await SelectPropertyCoreAsync(contract.PropertyId);
        ApplyContract(contract);
        TemplateName = contract.Title;
        BillTitle = contract.Title;
        SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == contract.CategoryId);
        SelectedSupplier = SupplierOptions().FirstOrDefault(s => s.Id == contract.SupplierId);

        // The amount of the newest bill is the usual one.
        var last = (await _billStore.ListByContractAsync(contract.Id)).LastOrDefault();
        if (last is not null)
        {
            Amounts.Load(new AmountSet(last.Net, last.VatPercent, last.Vat, last.Gross));
            var issued = last.IssuedOn;
            DayText = issued is { } date ? date.Day.ToString(CultureInfo.InvariantCulture) : string.Empty;
            DueOffsetText = issued is { } from && last.DueOn is { } due && due >= from
                ? (due.DayNumber - from.DayNumber).ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        foreach (var field in contract.CustomFields.Where(f => !f.IsDeleted).OrderBy(f => f.SortOrder))
        {
            _fieldRows.Add(CreateFieldRow(new CustomFieldDefinition { Name = field.Name, Kind = field.Kind, Target = field.Target }));
        }
    }

    private async Task PrepareFromBillAsync(Bill bill)
    {
        await SelectPropertyCoreAsync(bill.PropertyId);
        if (bill.ContractId is { } contractId && await _contractStore.GetAsync(contractId) is { } contract)
        {
            ApplyContract(contract);
        }

        TemplateName = string.IsNullOrWhiteSpace(bill.Title) ? _contract?.Title ?? string.Empty : bill.Title;
        BillTitle = bill.Title ?? string.Empty;
        SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == bill.CategoryId);
        SelectedSupplier = bill.SupplierId is { } supplierId ? SupplierOptions().FirstOrDefault(s => s.Id == supplierId) : SelectedSupplier;
        Amounts.Load(new AmountSet(bill.Net, bill.VatPercent, bill.Vat, bill.Gross));
        Notes = bill.Notes ?? string.Empty;
        StartMonth = bill.Period;
        SelectedKind = Kinds.First(k => k.Value.Equals(bill.Kind == BillKind.OneOff ? ScheduleKind.Once : ScheduleKind.Monthly));
        if (bill.Kind != BillKind.OneOff)
        {
            DayText = bill.IssuedOn is { } issued ? issued.Day.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        DueOffsetText = bill.IssuedOn is { } from && bill.DueOn is { } due && due >= from
            ? (due.DayNumber - from.DayNumber).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        _lines.AddRange(bill.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).Select(Copy));
        foreach (var value in bill.CustomFields)
        {
            _fieldRows.Add(CreateFieldRow(new CustomFieldDefinition { Name = value.Name, Kind = value.Kind, Target = CustomFieldTarget.Bill }));
        }
    }

    private async Task ShowExistingAsync(BillTemplate existing)
    {
        _contract = existing.ContractId is { } contractId ? await _contractStore.GetAsync(contractId) : null;
        _contracts = existing.PropertyId == Guid.Empty ? [] : await ContractsOfAsync(existing.PropertyId);
        _lines.Clear();
        _lines.AddRange(existing.Lines.Select(Copy));
        _fieldRows.Clear();
        foreach (var field in existing.CustomFields.Where(f => !f.IsDeleted).OrderBy(f => f.SortOrder))
        {
            _fieldRows.Add(CreateFieldRow(field));
        }

        TemplateName = existing.Name;
        BillTitle = existing.Title ?? string.Empty;
        Notes = existing.Notes ?? string.Empty;
        IsActive = existing.IsActive;
        SelectedProperty = _properties.Where(p => p.Id == existing.PropertyId).Select(PropertyOption).FirstOrDefault();
        SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == existing.CategoryId);
        SelectedContract = _contract is { } own ? new ChoiceOption(own.Id, own.Title, string.Empty) : null;
        SelectedSupplier = existing.SupplierId is { } supplierId ? SupplierOptions().FirstOrDefault(s => s.Id == supplierId) : null;
        CurrencySymbol = _localizer["Currency_Symbol_" + (_contract?.Currency ?? Currency.EUR)];
        Amounts.Load(existing is { Net: null, VatPercent: null, Gross: null }
            ? null
            : HomeUtilities.Core.Billing.Amounts.Complete(existing.Net, existing.VatPercent, existing.Gross).Result);

        var schedule = existing.Schedule;
        SelectedKind = Kinds.FirstOrDefault(k => k.Value.Equals(schedule.Kind)) ?? Kinds[0];
        EveryMonthsText = schedule.EveryMonths?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        StartMonth = schedule.StartMonth ?? YearMonth.From(_clock.Today);
        HasEnd = schedule.EndMonth is not null;
        EndMonth = schedule.EndMonth ?? StartMonth;
        DayText = schedule.DayOfMonth?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedTiming = Timings.FirstOrDefault(t => t.Value.Equals(existing.Timing)) ?? Timings[0];
        DueOffsetText = existing.DueDayOffset?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        RemindText = existing.RemindDaysBefore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        RebuildLines();
        RebuildFields();
    }

    private async Task SelectPropertyCoreAsync(Guid propertyId)
    {
        SelectedProperty = _properties.Where(p => p.Id == propertyId).Select(PropertyOption).FirstOrDefault();
        _contracts = await ContractsOfAsync(propertyId);
        _contract = null;
        SelectedContract = null;
    }

    private async Task<IReadOnlyList<Contract>> ContractsOfAsync(Guid propertyId) =>
    [
        .. (await _contractStore.ListByPropertyAsync(propertyId))
            .Where(c => !c.IsDeleted && !c.IsArchived)
            .OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase),
    ];

    /// <summary>Takes what a template inherits from its contract: category, supplier and timing (unless the user chose them).</summary>
    private void ApplyContract(Contract contract)
    {
        _contract = contract;
        SelectedContract = new ChoiceOption(contract.Id, contract.Title, string.Empty);
        CurrencySymbol = _localizer["Currency_Symbol_" + contract.Currency];
        if (!_categoryTouched)
        {
            SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == contract.CategoryId);
        }

        if (!_supplierTouched)
        {
            SelectedSupplier = SupplierOptions().FirstOrDefault(s => s.Id == contract.SupplierId);
        }

        SelectedTiming = Timings.FirstOrDefault(t => t.Value.Equals(contract.BillingTiming)) ?? Timings[0];
    }

    private string TitleText() => IsNew
        ? _localizer["BillTemplateNewTitle"]
        : _localizer.Get("BillTemplateEditTitle", string.IsNullOrWhiteSpace(TemplateName) ? _template.Name : TemplateName.Trim());

    // ---- Choosers -------------------------------------------------------------------------------------------------

    private ChoiceOption PropertyOption(Property property) => new(property.Id, property.Name, string.Empty);

    private IReadOnlyList<ChoiceOption> PropertyOptions() =>
        [.. _properties.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(PropertyOption)];

    private IReadOnlyList<ChoiceOption> CategoryOptions() =>
    [
        .. _categories
            .Where(c => !c.IsArchived || c.Id == SelectedCategory?.Id || c.Id == _template.CategoryId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChoiceOption(c.Id, CategoryNames.DisplayName(c, _localizer), c.Color)),
    ];

    private IReadOnlyList<ChoiceOption> SupplierOptions() =>
        [.. _suppliers.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(s => new ChoiceOption(s.Id, s.Name, string.Empty))];

    [RelayCommand]
    private Task ChoosePropertyAsync() => RunSafeAsync(async () =>
    {
        var options = PropertyOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseProperty"], [.. options.Select(o => o.Name)]);
        if (picked is null || options.FirstOrDefault(o => o.Name == picked) is not { } option || option.Id == SelectedProperty?.Id)
        {
            return;
        }

        await SelectPropertyCoreAsync(option.Id);
        UpdatePreview();
    });

    [RelayCommand]
    private Task ChooseContractAsync() => RunSafeAsync(async () =>
    {
        if (SelectedProperty is null)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillTemplateChoosePropertyFirst"]);
            return;
        }

        var none = _localizer["BillContractNone"];
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseContract"], [none, .. _contracts.Select(c => c.Title)]);
        if (picked is null)
        {
            return;
        }

        if (picked == none)
        {
            _contract = null;
            SelectedContract = null;
            CurrencySymbol = _localizer["Currency_Symbol_" + Currency.EUR];
            return;
        }

        if (_contracts.FirstOrDefault(c => c.Title == picked) is { } contract)
        {
            ApplyContract(contract);
        }
    });

    [RelayCommand]
    private Task ChooseCategoryAsync() => RunSafeAsync(async () =>
    {
        var options = CategoryOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseCategory"], [.. options.Select(o => o.Name)]);
        if (picked is not null && options.FirstOrDefault(o => o.Name == picked) is { } option)
        {
            SelectedCategory = option;
        }
    });

    [RelayCommand]
    private Task ChooseSupplierAsync() => RunSafeAsync(async () =>
    {
        var none = _localizer["BillNoSupplier"];
        var options = SupplierOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseSupplier"], [none, .. options.Select(o => o.Name)]);
        if (picked is null)
        {
            return;
        }

        SelectedSupplier = picked == none ? null : options.FirstOrDefault(o => o.Name == picked);
    });

    [RelayCommand]
    private void PreviousStart() => StartMonth = StartMonth.AddMonths(-1);

    [RelayCommand]
    private void NextStart() => StartMonth = StartMonth.AddMonths(1);

    [RelayCommand]
    private void PreviousEnd() => EndMonth = EndMonth.AddMonths(-1);

    [RelayCommand]
    private void NextEnd() => EndMonth = EndMonth.AddMonths(1);

    // ---- Lines ----------------------------------------------------------------------------------------------------

    [RelayCommand]
    private void ToggleLines() => LinesExpanded = !LinesExpanded;

    [RelayCommand]
    private Task AddLineAsync() => RunSafeAsync(() => EditLineAsync(null));

    private async Task EditLineAsync(BillLine? line)
    {
        var dialog = new BillLineEditorViewModel(_localizer, line, _categories, [], _clock.Today);
        if (!await Dialogs.ShowEditorAsync(dialog) || dialog.Result is not { } result)
        {
            return;
        }

        var index = line is null ? -1 : _lines.FindIndex(l => l.Id == line.Id);
        if (index >= 0)
        {
            _lines[index] = result;
        }
        else
        {
            _lines.Add(result);
        }

        LinesExpanded = true;
        RebuildLines();
        MarkDirty();
    }

    private Task RemoveLineAsync(BillLine line) => RunSafeAsync(async () =>
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", line.Description),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _lines.RemoveAll(l => l.Id == line.Id);
        RebuildLines();
        MarkDirty();
    });

    private void RebuildLines()
    {
        var symbol = CurrencySymbol.Length > 0 ? CurrencySymbol : _localizer["Currency_Symbol_" + Currency.EUR];
        LineRows =
        [
            .. _lines.Select(line => new BillLineRowViewModel(
                line,
                BillText.Money(line.Gross, symbol),
                LineDetail(line),
                Texts.DeleteLineText,
                l => _ = RunSafeAsync(() => EditLineAsync(l)),
                l => _ = RemoveLineAsync(l))),
        ];
    }

    private string LineDetail(BillLine line)
    {
        if (line.Quantity is not { } quantity || line.UnitPrice is not { } price)
        {
            return string.Empty;
        }

        var quantityText = DecimalInput.Format(quantity, CultureInfo.CurrentCulture, 4) + (string.IsNullOrWhiteSpace(line.Unit) ? string.Empty : " " + line.Unit);
        return _localizer.Get("BillLineSummary", quantityText, DecimalInput.Format(price, CultureInfo.CurrentCulture, 6));
    }

    private static BillLine Copy(BillTemplateLine line) => line.ToBillLine(Guid.Empty, 0);

    private static BillLine Copy(BillLine line) => new()
    {
        Description = line.Description,
        Quantity = line.Quantity,
        Unit = line.Unit,
        UnitPrice = line.UnitPrice,
        Net = line.Net,
        VatPercent = line.VatPercent,
        Vat = line.Vat,
        Gross = line.Gross,
        CategoryId = line.CategoryId,
        Note = line.Note,
    };

    // ---- Custom fields --------------------------------------------------------------------------------------------

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
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", row.DisplayText),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _fieldRows.Remove(row);
        RebuildFields();
        MarkDirty();
    });

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

    // ---- Building the template and the preview --------------------------------------------------------------------

    /// <summary>Reads the form into a template; unreadable numbers are added to <paramref name="errors"/>.</summary>
    private BillTemplate BuildTemplate(List<ValidationError> errors)
    {
        AmountSet? amounts = null;
        if (!string.IsNullOrWhiteSpace(Amounts.NetText) || !string.IsNullOrWhiteSpace(Amounts.VatPercentText) || !string.IsNullOrWhiteSpace(Amounts.GrossText))
        {
            errors.AddRange(Amounts.TryRead(out amounts));
        }

        var everyMonths = ReadInt(EveryMonthsText, "Schedule.EveryMonths", errors);
        var day = ReadInt(DayText, "Schedule.DayOfMonth", errors);
        var dueOffset = ReadInt(DueOffsetText, nameof(BillTemplate.DueDayOffset), errors);
        var remind = ReadInt(RemindText, nameof(BillTemplate.RemindDaysBefore), errors);
        var kind = SelectedKind?.As<ScheduleKind>() ?? ScheduleKind.Once;
        var scheduled = kind != ScheduleKind.Once;

        var template = new BillTemplate
        {
            Id = _template.Id,
            CreatedAt = _template.CreatedAt,
            UpdatedAt = _template.UpdatedAt,
            LastGeneratedPeriod = _template.LastGeneratedPeriod,
            Name = TemplateName.Trim(),
            PropertyId = SelectedProperty?.Id ?? Guid.Empty,
            CategoryId = SelectedCategory?.Id ?? Guid.Empty,
            ContractId = SelectedContract is { } contract && contract.Id != Guid.Empty ? contract.Id : null,
            SupplierId = SelectedSupplier is { } supplier && supplier.Id != Guid.Empty ? supplier.Id : null,
            Title = string.IsNullOrWhiteSpace(BillTitle) ? null : BillTitle.Trim(),
            Net = amounts?.Net,
            VatPercent = amounts?.VatPercent,
            Gross = amounts?.Gross,
            Timing = SelectedTiming?.As<BillingTiming>() ?? BillingTiming.InArrears,
            DueDayOffset = dueOffset,
            RemindDaysBefore = remind,
            IsActive = IsActive,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Schedule = new Schedule
            {
                Kind = kind,
                EveryMonths = kind == ScheduleKind.EveryNMonths ? everyMonths : null,
                StartMonth = scheduled ? StartMonth : null,
                EndMonth = scheduled && HasEnd ? EndMonth : null,
                DayOfMonth = scheduled ? day : null,
            },
        };
        template.Lines = [.. _lines.Select(BillTemplateLine.From)];
        template.CustomFields =
        [
            .. _fieldRows.Select((row, i) => new CustomFieldDefinition
            {
                Id = row.Id,
                CreatedAt = row.CreatedAt,
                OwnerId = template.Id,
                Name = row.Name.Trim(),
                Kind = row.Kind,
                Target = row.Target,
                SortOrder = i,
            }),
        ];
        return template;
    }

    private static int? ReadInt(string text, string field, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new(CommonErrorCodes.NumberInvalid, field));
        return null;
    }

    /// <summary>"Next drafts: 11.2026 (issued 05.11.2026, due 19.11.2026), 12.2026 …" from what is typed so far.</summary>
    private void UpdatePreview()
    {
        var errors = new List<ValidationError>();
        var template = BuildTemplate(errors);
        if (template.Schedule.Kind == ScheduleKind.Once)
        {
            PreviewText = _localizer["BillTemplatePreviewOnce"];
            return;
        }

        if (errors.Count > 0 || !BillTemplateValidator.ValidateSchedule(template.Schedule).IsValid)
        {
            PreviewText = string.Empty;
            return;
        }

        var items = BillTemplateScheduler.UpcomingPeriods(template, _clock.Today, 3)
            .Select(period => PreviewItem(template, period))
            .ToList();
        PreviewText = items.Count == 0
            ? _localizer["BillTemplatePreviewNone"]
            : _localizer.Get("BillTemplatePreview", string.Join(", ", items) + (items.Count == 3 ? " …" : string.Empty));
    }

    private string PreviewItem(BillTemplate template, YearMonth period)
    {
        var issued = BillTemplateScheduler.IssueDate(template, period);
        var due = BillTemplateScheduler.DueDate(template, period);
        return (issued, due) switch
        {
            ({ } i, { } d) => _localizer.Get("BillTemplatePreviewIssuedDue", BillText.Period(period), BillText.Date(i), BillText.Date(d)),
            ({ } i, null) => _localizer.Get("BillTemplatePreviewIssued", BillText.Period(period), BillText.Date(i)),
            _ => BillText.Period(period),
        };
    }

    partial void OnPreviewTextChanged(string value) => OnPropertyChanged(nameof(HasPreview));

    private static YearMonth Clamp(DateOnly date) =>
        date.Year < ChargesViewModel.EarliestMonth.Year ? ChargesViewModel.EarliestMonth
        : date.Year > ChargesViewModel.LatestMonth.Year ? ChargesViewModel.LatestMonth
        : YearMonth.From(date);

    private string ErrorOf(string field) => Errors.GetValueOrDefault(field, string.Empty);

    private void OnAmountsEdited() => MarkDirty();

    private void OnOwnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not { } name)
        {
            return;
        }

        if (name == nameof(IsNew))
        {
            OnPropertyChanged(nameof(ShowPropertyChooser));
        }

        if (!_loading && PreviewFields.Contains(name))
        {
            UpdatePreview();
        }

        if (!DirtyFields.Contains(name))
        {
            return;
        }

        if (!_loading)
        {
            switch (name)
            {
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
}
