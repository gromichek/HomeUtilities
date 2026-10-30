using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>A sheet of the picked workbook.</summary>
/// <param name="Name">Sheet name.</param>
/// <param name="Text">Name with the number of rows.</param>
public sealed record ImportSheetOption(string Name, string Text);

/// <summary>An existing import profile in the picker of the target step; <see cref="Profile"/> is null for "New profile".</summary>
public sealed record ImportProfileOption(SavedImportProfile? Profile, string Text);

/// <summary>An entry of the currency rule picker.</summary>
public sealed record ImportCurrencyChoice(ImportCurrencyRule Value, string Text);

/// <summary>An entry of the row filter picker of the preview.</summary>
public sealed record ImportFilterChoice(ImportRowFilter Value, string Text);

/// <summary>The steps of the wizard.</summary>
public enum ImportWizardStep
{
    /// <summary>Pick the workbook and the sheet.</summary>
    File = 1,

    /// <summary>Pick the profile and the target contract.</summary>
    Target = 2,

    /// <summary>Map the columns.</summary>
    Mapping = 3,

    /// <summary>Preview, dry run and the import button.</summary>
    Preview = 4,

    /// <summary>Summary.</summary>
    Done = 5,
}

/// <summary>Which rows of the dry run are listed.</summary>
public enum ImportRowFilter
{
    /// <summary>All rows.</summary>
    All = 0,

    /// <summary>Rows with errors.</summary>
    Errors = 1,

    /// <summary>Rows with warnings.</summary>
    Warnings = 2,
}

/// <summary>
/// The Excel import wizard (Settings → Import from Excel): file and sheet → profile and contract → column mapping →
/// preview with a dry run → import. All heavy work (reading the sheet, planning) runs off the UI thread.
/// </summary>
public sealed partial class ImportWizardViewModel : ViewModelBase
{
    private const int PreviewRowCount = 15;
    private const int MaxRowNumber = 1_048_576;

    private readonly IImportFileService _files;
    private readonly IWorkbookReader _reader;
    private readonly IPropertyStore _properties;
    private readonly IContractStore _contracts;
    private readonly IMeterStore _meters;
    private readonly IBillStore _bills;
    private readonly IEqualizedPlanStore _plans;
    private readonly IImportProfileStore _profiles;
    private readonly IImportBatchStore _batches;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _snackbar;
    private readonly ILocalizer _localizer;

    private byte[] _workbook = [];
    private string _fileName = string.Empty;
    private SheetData? _sheet;
    private Contract? _contract;
    private IReadOnlyList<Meter> _propertyMeters = [];
    private IReadOnlyList<ContractMeter> _propertyLinks = [];
    private IReadOnlyList<LinkStartMove> _linkMoves = [];
    private List<ImportRowOverride> _rowOverrides = [];
    private List<ReplacementEdit> _replacementEdits = [];
    private ImportProfile? _plannedProfile;
    private readonly Dictionary<Guid, string> _registerLabels = [];
    private ImportPlan? _plan;
    private IReadOnlyList<ImportPlanRowViewModel> _allPlanRows = [];
    private ImportProfile? _loadedProfile;
    private List<int> _notesColumns = [];
    private bool _loading;
    private bool _applying;
    private Task _loadTask = Task.CompletedTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFileStep), nameof(IsTargetStep), nameof(IsMappingStep), nameof(IsPreviewStep), nameof(IsDoneStep), nameof(StepText), nameof(ShowBack), nameof(ShowNext), nameof(IsScrollStep), nameof(ShowImport), nameof(ShowOpenImports))]
    private ImportWizardStep _step = ImportWizardStep.File;

    [ObservableProperty]
    private string _fileText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ImportSheetOption> _sheets = [];

    [ObservableProperty]
    private ImportSheetOption? _selectedSheet;

    [ObservableProperty]
    private IReadOnlyList<ImportProfileOption> _profileChoices = [];

    [ObservableProperty]
    private ImportProfileOption? _selectedProfile;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ChoiceOption> _propertyChoices = [];

    [ObservableProperty]
    private ChoiceOption? _selectedProperty;

    [ObservableProperty]
    private IReadOnlyList<ChoiceOption> _contractChoices = [];

    [ObservableProperty]
    private ChoiceOption? _selectedContract;

    [ObservableProperty]
    private IReadOnlyList<string> _meterLines = [];

    [ObservableProperty]
    private string _targetHintText = string.Empty;

    [ObservableProperty]
    private string _headerRowText = "1";

    [ObservableProperty]
    private string _firstRowText = "2";

    [ObservableProperty]
    private string _lastRowText = string.Empty;

    [ObservableProperty]
    private string _paidMarker = ImportProfile.DefaultPaidMarker;

    [ObservableProperty]
    private IReadOnlyList<ImportCurrencyChoice> _currencyRules = [];

    [ObservableProperty]
    private ImportCurrencyChoice? _selectedCurrencyRule;

    [ObservableProperty]
    private IReadOnlyList<ImportColumnOption> _columnOptions = [];

    [ObservableProperty]
    private IReadOnlyList<ImportColumnOption> _previewHeader = [];

    [ObservableProperty]
    private IReadOnlyList<ImportSheetPreviewRow> _previewRows = [];

    [ObservableProperty]
    private ImportColumnPickerViewModel _periodPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _totalPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _netPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _vatPercentPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _vatPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _paidPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _paidDatePicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _quantityPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _totalFallbackPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _netFallbackPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private ImportColumnPickerViewModel _vatFallbackPicker = ImportColumnPickerViewModel.Unmapped;

    [ObservableProperty]
    private IReadOnlyList<ImportPeriodRuleChoice> _periodRules = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDayLimit))]
    private ImportPeriodRuleChoice? _selectedPeriodRule;

    [ObservableProperty]
    private string _periodDayLimitText = ImportPeriodResolver.DefaultDayLimit.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool _dateIsPaymentDate;

    [ObservableProperty]
    private bool _detectReplacements = true;

    [ObservableProperty]
    private IReadOnlyList<ImportRuleChangeRowViewModel> _ruleChanges = [];

    [ObservableProperty]
    private IReadOnlyList<ImportReplacementRowViewModel> _replacementRows = [];

    [ObservableProperty]
    private IReadOnlyList<ImportRegisterMappingViewModel> _registerMappings = [];

    [ObservableProperty]
    private string _notesSummary = string.Empty;

    [ObservableProperty]
    private string _mappingError = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _planProblemText = string.Empty;

    /// <summary>"N rows are before the contract's start date …"; empty when no row is.</summary>
    [ObservableProperty]
    private string _beforeStartText = string.Empty;

    /// <summary>The text of the "Set contract start to …" button; empty when there is nothing to set.</summary>
    [ObservableProperty]
    private string _setContractStartText = string.Empty;

    /// <summary>"N meter links start after the earliest imported data …"; empty when no link does.</summary>
    [ObservableProperty]
    private string _linkStartText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ImportFilterChoice> _filters = [];

    [ObservableProperty]
    private ImportFilterChoice? _selectedFilter;

    [ObservableProperty]
    private IReadOnlyList<ImportPlanRowViewModel> _planRows = [];

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _canImport;

    public ImportWizardViewModel(
        IImportFileService files,
        IWorkbookReader reader,
        IPropertyStore properties,
        IContractStore contracts,
        IMeterStore meters,
        IBillStore bills,
        IEqualizedPlanStore plans,
        IImportProfileStore profiles,
        IImportBatchStore batches,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar,
        ILocalizer localizer)
    {
        _files = files;
        _reader = reader;
        _properties = properties;
        _contracts = contracts;
        _meters = meters;
        _bills = bills;
        _plans = plans;
        _profiles = profiles;
        _batches = batches;
        _dialogs = dialogs;
        _navigation = navigation;
        _snackbar = snackbar;
        _localizer = localizer;
        RefreshTexts();
    }

    public bool IsFileStep => Step == ImportWizardStep.File;

    public bool IsTargetStep => Step == ImportWizardStep.Target;

    public bool IsMappingStep => Step == ImportWizardStep.Mapping;

    public bool IsPreviewStep => Step == ImportWizardStep.Preview;

    public bool IsDoneStep => Step == ImportWizardStep.Done;

    /// <summary>True for the steps shown in a scrolling form; the preview lists its rows itself.</summary>
    public bool IsScrollStep => Step != ImportWizardStep.Preview;

    /// <summary>True on the preview step, where the Import button is the one primary action.</summary>
    public bool ShowImport => Step == ImportWizardStep.Preview;

    /// <summary>True on the last step.</summary>
    public bool ShowOpenImports => Step == ImportWizardStep.Done;

    /// <summary>"Step 2 of 4: Contract".</summary>
    public string StepText => _localizer.Get("ImportStepOf", (int)Math.Min((int)Step, 4), 4, _localizer[$"ImportStep_{Step}"]);

    public bool ShowBack => Step is > ImportWizardStep.File and < ImportWizardStep.Done;

    public bool ShowNext => Step is < ImportWizardStep.Preview;

    public string NextText { get; private set; } = string.Empty;

    public string BackText { get; private set; } = string.Empty;

    public string ImportText { get; private set; } = string.Empty;

    public string PickFileText { get; private set; } = string.Empty;

    public string FileHint { get; private set; } = string.Empty;

    public string SheetsLabel { get; private set; } = string.Empty;

    public string ProfileLabel { get; private set; } = string.Empty;

    public string ProfileNameLabel { get; private set; } = string.Empty;

    public string PropertyLabel { get; private set; } = string.Empty;

    public string ContractLabel { get; private set; } = string.Empty;

    public string MetersHeader { get; private set; } = string.Empty;

    public string ShowContractText { get; private set; } = string.Empty;

    public string MapHint { get; private set; } = string.Empty;

    public string RowsHeader { get; private set; } = string.Empty;

    public string HeaderRowLabel { get; private set; } = string.Empty;

    public string FirstRowLabel { get; private set; } = string.Empty;

    public string LastRowLabel { get; private set; } = string.Empty;

    public string PaidMarkerLabel { get; private set; } = string.Empty;

    public string CurrencyLabel { get; private set; } = string.Empty;

    public string ColumnsHeader { get; private set; } = string.Empty;

    public string ReadingsHeader { get; private set; } = string.Empty;

    public string NotesLabel { get; private set; } = string.Empty;

    public string ChooseNotesText { get; private set; } = string.Empty;

    public string GuessText { get; private set; } = string.Empty;

    public string SheetPreviewHeader { get; private set; } = string.Empty;

    public string FilterLabel { get; private set; } = string.Empty;

    public string OpenImportsText { get; private set; } = string.Empty;

    public string NoRowsText { get; private set; } = string.Empty;

    public string PeriodRuleLabel { get; private set; } = string.Empty;

    public string PeriodRuleHint { get; private set; } = string.Empty;

    public string DayLimitLabel { get; private set; } = string.Empty;

    public string DateIsPaymentDateLabel { get; private set; } = string.Empty;

    public string RuleChangesHeader { get; private set; } = string.Empty;

    public string AddRuleChangeText { get; private set; } = string.Empty;

    public string DetectReplacementsLabel { get; private set; } = string.Empty;

    public string ReplacementsHeader { get; private set; } = string.Empty;

    public string ReplacementsHint { get; private set; } = string.Empty;

    public string RecalculateText { get; private set; } = string.Empty;

    /// <summary>True when the selected period rule uses the day limit.</summary>
    public bool ShowDayLimit => SelectedPeriodRule?.Value == ImportPeriodRule.PreviousMonthIfDayUpTo;

    /// <summary>True when the dry run proposes meter replacements.</summary>
    public bool HasReplacements => ReplacementRows.Count > 0;

    /// <summary>True when the filtered list has no rows.</summary>
    public bool ShowNoRows => PlanRows.Count == 0 && _plan is not null;

    /// <summary>True when the dry run could not be made because of a problem with the mapping.</summary>
    public bool HasPlanProblem => PlanProblemText.Length > 0;

    /// <summary>True when a mapping error is shown.</summary>
    public bool HasMappingError => MappingError.Length > 0;

    /// <summary>True when there are no properties or contracts to import into yet.</summary>
    public bool HasTargetHint => TargetHintText.Length > 0;

    /// <summary>True when the contract has meters whose registers can receive readings.</summary>
    public bool HasMeters => MeterLines.Count > 0;

    /// <summary>The sheet columns imported as notes (1-based).</summary>
    public IReadOnlyList<int> NotesColumns => _notesColumns;

    /// <summary>Completes when the contracts and meters of the last property / contract choice are loaded.</summary>
    public Task WhenLoadedAsync() => _loadTask;

    /// <summary>The last dry run (null before the preview step).</summary>
    public ImportPlan? Plan => _plan;

    /// <summary>Whether a new profile is being made (as opposed to reusing a saved one).</summary>
    public bool IsNewProfile => SelectedProfile?.Profile is null;

    partial void OnReplacementRowsChanged(IReadOnlyList<ImportReplacementRowViewModel> value) => OnPropertyChanged(nameof(HasReplacements));

    partial void OnPlanRowsChanged(IReadOnlyList<ImportPlanRowViewModel> value) => OnPropertyChanged(nameof(ShowNoRows));

    partial void OnPlanProblemTextChanged(string value) => OnPropertyChanged(nameof(HasPlanProblem));

    /// <summary>True when rows lie before the contract's start date and the start can be moved.</summary>
    public bool HasBeforeStart => BeforeStartText.Length > 0;

    partial void OnBeforeStartTextChanged(string value) => OnPropertyChanged(nameof(HasBeforeStart));

    /// <summary>True when meter links of the contract start after the earliest imported data.</summary>
    public bool HasLinkStart => LinkStartText.Length > 0;

    /// <summary>True when the button that moves the contract start and/or the link starts is offered.</summary>
    public bool HasStartFix => SetContractStartText.Length > 0;

    partial void OnLinkStartTextChanged(string value) => OnPropertyChanged(nameof(HasLinkStart));

    partial void OnSetContractStartTextChanged(string value) => OnPropertyChanged(nameof(HasStartFix));

    partial void OnMappingErrorChanged(string value) => OnPropertyChanged(nameof(HasMappingError));

    partial void OnTargetHintTextChanged(string value) => OnPropertyChanged(nameof(HasTargetHint));

    partial void OnMeterLinesChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasMeters));

    partial void OnSelectedFilterChanged(ImportFilterChoice? value) => ApplyFilter();

    partial void OnSelectedProfileChanged(ImportProfileOption? value)
    {
        OnPropertyChanged(nameof(IsNewProfile));
        if (_loading || value?.Profile is not { } saved)
        {
            return;
        }

        ProfileName = saved.Name;
        _ = ReselectTargetFromProfileAsync(saved);
    }

    partial void OnSelectedPropertyChanged(ChoiceOption? value)
    {
        if (!_loading)
        {
            _loadTask = RunSafeAsync(() => LoadContractsAsync(value, null));
        }
    }

    partial void OnSelectedContractChanged(ChoiceOption? value)
    {
        if (!_loading)
        {
            _loadTask = RunSafeAsync(LoadContractMetersAsync);
        }
    }

    public override Task OnAppearingAsync() => RunSafeAsync(async () =>
    {
        if (_properties is null || PropertyChoices.Count > 0 || Step != ImportWizardStep.File)
        {
            return;
        }

        await LoadPropertiesAsync();
    });

    /// <summary>Lets the owner pick an .xlsx file and lists its sheets.</summary>
    [RelayCommand]
    private Task PickFileAsync() => RunSafeAsync(async () =>
    {
        var picked = await _files.PickWorkbookAsync();
        if (picked is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await using (picked.Content)
            {
                _workbook = await Task.Run(async () =>
                {
                    using var copy = new MemoryStream();
                    await picked.Content.CopyToAsync(copy);
                    return copy.ToArray();
                });
            }

            var sheets = await Task.Run(() =>
            {
                using var stream = new MemoryStream(_workbook, writable: false);
                return _reader.ListSheets(stream);
            });

            _fileName = picked.Name;
            FileText = _localizer.Get("ImportFileChosen", picked.Name);
            Sheets = [.. sheets.Select(s => new ImportSheetOption(s.Name, _localizer.Get("ImportSheetRows", s.Name, s.RowCount)))];
            SelectedSheet = Sheets.Count == 1 ? Sheets[0] : null;
            _sheet = null;
        }
        catch (InvalidDataException)
        {
            _workbook = [];
            Sheets = [];
            SelectedSheet = null;
            FileText = string.Empty;
            await _dialogs.AlertAsync(_localizer["ImportTitle"], _localizer["ImportNotAWorkbook"]);
        }
        finally
        {
            IsBusy = false;
        }
    });

    /// <summary>Goes to the next step, doing the work that step needs.</summary>
    [RelayCommand]
    private Task NextAsync() => RunSafeAsync(async () =>
    {
        switch (Step)
        {
            case ImportWizardStep.File:
                if (SelectedSheet is null)
                {
                    await _dialogs.AlertAsync(_localizer["ImportTitle"], _localizer["ImportChooseSheet"]);
                    return;
                }

                await EnterTargetAsync();
                Step = ImportWizardStep.Target;
                break;

            case ImportWizardStep.Target:
                if (SelectedContract is null)
                {
                    await _dialogs.AlertAsync(_localizer["ImportTitle"], _localizer["ImportChooseContract"]);
                    return;
                }

                IsBusy = true;
                try
                {
                    await EnterMappingAsync();
                }
                finally
                {
                    IsBusy = false;
                }

                Step = ImportWizardStep.Mapping;
                break;

            case ImportWizardStep.Mapping:
                if (!TryBuildProfile(out var profile))
                {
                    return;
                }

                IsBusy = true;
                try
                {
                    await EnterPreviewAsync(profile);
                }
                finally
                {
                    IsBusy = false;
                }

                Step = ImportWizardStep.Preview;
                break;
        }
    });

    /// <summary>Goes one step back.</summary>
    [RelayCommand]
    private void Back()
    {
        if (ShowBack)
        {
            Step = (ImportWizardStep)((int)Step - 1);
        }
    }

    /// <summary>Guesses the columns again from the header texts.</summary>
    [RelayCommand]
    private Task GuessAsync() => RunSafeAsync(async () =>
    {
        if (_sheet is null || !TryParseRows(out var header, out _, out _))
        {
            return;
        }

        var registerIds = RegisterMappings.Select(r => r.RegisterId).ToList();
        var guessed = ImportColumnGuesser.Guess(_sheet, header, registerIds);
        guessed.ContractId = _contract?.Id ?? Guid.Empty;
        guessed.CurrencyRule = SelectedCurrencyRule?.Value ?? guessed.CurrencyRule;
        CopyPeriodSettings(guessed);
        ApplyProfileToUi(guessed, keepRows: true);
        await Task.CompletedTask;
    });

    partial void OnHeaderRowTextChanged(string value)
    {
        // The column names come from the header row: show the new ones while the owner types.
        if (_applying || _sheet is null || !int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var header) || header > _sheet.RowCount)
        {
            return;
        }

        RebuildColumnOptions(header);
        UpdateNotesSummary();
    }

    /// <summary>Opens the multi-select dialog for the columns that are imported as notes.</summary>
    [RelayCommand]
    private Task ChooseNotesAsync() => RunSafeAsync(async () =>
    {
        var items = ColumnOptions.Where(o => o.Index > 0)
            .Select(o => new MultiSelectItemViewModel(ColumnToId(o.Index), o.Text, string.Empty, _notesColumns.Contains(o.Index)));
        var dialog = new MultiSelectDialogViewModel(_localizer, _localizer["ImportNotesLabel"], items, [.. _notesColumns.Select(ColumnToId)]);
        if (await _dialogs.ShowEditorAsync(dialog))
        {
            _notesColumns = [.. dialog.SelectedIds.Select(IdToColumn).Order()];
            UpdateNotesSummary();
        }
    });

    /// <summary>Opens the contract (or its property's contracts) so that a missing meter or contract can be created.</summary>
    [RelayCommand]
    private Task ShowContractAsync() => RunSafeAsync(async () =>
    {
        if (SelectedProperty is not { } property)
        {
            await _navigation.GoToAsync(Routes.Properties);
            return;
        }

        var parameters = new Dictionary<string, object> { ["propertyId"] = property.Id };
        if (SelectedContract is { } contract)
        {
            parameters["id"] = contract.Id;
        }

        await _navigation.GoToAsync(Routes.ContractEdit, parameters);
    });

    /// <summary>Runs the import after one confirmation.</summary>
    [RelayCommand]
    private Task ImportAsync() => RunSafeAsync(async () =>
    {
        if (_plan is null || _contract is null || !CanImport || !TryBuildProfile(out var profile))
        {
            return;
        }

        var counts = _plan.Counts;
        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["ImportTitle"],
            _localizer.Get("ImportConfirm", counts.Readings, counts.Bills, counts.Payments, counts.Replacements),
            _localizer["ImportAction"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var saved = await SaveProfileAsync(profile);
            var batch = await _batches.ApplyAsync(new ImportApplyRequest(_plan, _fileName, profile.SheetName, _contract.Id, saved.Id));
            ResultText = _localizer.Get("ImportDone", batch.ReadingCount, batch.BillCount, batch.PaymentCount, batch.SkippedCount, batch.ReplacementCount);
            CanImport = false;
            Step = ImportWizardStep.Done;
        }
        finally
        {
            IsBusy = false;
        }
    });

    /// <summary>Opens the list of imports (with Undo).</summary>
    [RelayCommand]
    private Task OpenImportsAsync() => RunSafeAsync(() => _navigation.GoToAsync(Routes.Imports));

    protected override void RefreshTexts()
    {
        Title = _localizer["ImportTitle"];
        NextText = _localizer["ImportNext"];
        BackText = _localizer["ImportBack"];
        ImportText = _localizer["ImportAction"];
        PickFileText = _localizer["ImportPickFile"];
        FileHint = _localizer["ImportFileHint"];
        SheetsLabel = _localizer["ImportSheetsLabel"];
        ProfileLabel = _localizer["ImportProfileLabel"];
        ProfileNameLabel = _localizer["ImportProfileNameLabel"];
        PropertyLabel = _localizer["BillPropertyLabel"];
        ContractLabel = _localizer["BillContractLabel"];
        MetersHeader = _localizer["ImportMetersHeader"];
        ShowContractText = _localizer["ImportShowContract"];
        MapHint = _localizer["ImportMapHint"];
        RowsHeader = _localizer["ImportRowsHeader"];
        HeaderRowLabel = _localizer["ImportHeaderRowLabel"];
        FirstRowLabel = _localizer["ImportFirstRowLabel"];
        LastRowLabel = _localizer["ImportLastRowLabel"];
        PaidMarkerLabel = _localizer["ImportPaidMarkerLabel"];
        CurrencyLabel = _localizer["ImportCurrencyLabel"];
        ColumnsHeader = _localizer["ImportColumnsHeader"];
        ReadingsHeader = _localizer["ImportReadingsHeader"];
        NotesLabel = _localizer["ImportNotesLabel"];
        ChooseNotesText = _localizer["ImportChooseNotes"];
        GuessText = _localizer["ImportGuess"];
        SheetPreviewHeader = _localizer["ImportSheetPreview"];
        FilterLabel = _localizer["ImportFilterLabel"];
        OpenImportsText = _localizer["ImportOpenImports"];
        NoRowsText = _localizer["ImportNoRows"];

        PeriodRuleLabel = _localizer["ImportPeriodRuleLabel"];
        PeriodRuleHint = _localizer["ImportPeriodRuleHint"];
        DayLimitLabel = _localizer["ImportDayLimitLabel"];
        DateIsPaymentDateLabel = _localizer["ImportDateIsPaymentDate"];
        RuleChangesHeader = _localizer["ImportRuleChangesHeader"];
        AddRuleChangeText = _localizer["ImportAddRuleChange"];
        DetectReplacementsLabel = _localizer["ImportDetectReplacements"];
        ReplacementsHeader = _localizer["ImportReplacementsHeader"];
        ReplacementsHint = _localizer["ImportReplacementsHint"];
        RecalculateText = _localizer["ImportRecalculate"];

        var periodRule = SelectedPeriodRule?.Value ?? ImportPeriodRule.MonthOfDate;
        PeriodRules = [.. Enum.GetValues<ImportPeriodRule>().Select(r => new ImportPeriodRuleChoice(r, _localizer[$"ImportPeriodRule_{r}"]))];
        SelectedPeriodRule = PeriodRules.First(r => r.Value == periodRule);

        var rule = SelectedCurrencyRule?.Value ?? ImportCurrencyRule.LvlBefore2014;
        CurrencyRules = [.. Enum.GetValues<ImportCurrencyRule>().Select(r => new ImportCurrencyChoice(r, _localizer[$"ImportCurrencyRule_{r}"]))];
        SelectedCurrencyRule = CurrencyRules.First(r => r.Value == rule);

        var filter = SelectedFilter?.Value ?? ImportRowFilter.All;
        Filters = [.. Enum.GetValues<ImportRowFilter>().Select(f => new ImportFilterChoice(f, _localizer[$"ImportFilter_{f}"]))];
        SelectedFilter = Filters.First(f => f.Value == filter);

        UpdateNotesSummary();
        OnPropertyChanged(string.Empty);
    }

    private async Task LoadPropertiesAsync()
    {
        _loading = true;
        try
        {
            var properties = await _properties.ListAsync(false);
            PropertyChoices = [.. properties.Select(p => new ChoiceOption(p.Id, p.Name, string.Empty))];
            ProfileChoices = await BuildProfileChoicesAsync();
            SelectedProfile = ProfileChoices[0];
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task<IReadOnlyList<ImportProfileOption>> BuildProfileChoicesAsync()
    {
        var saved = await _profiles.ListAsync();
        return
        [
            new ImportProfileOption(null, _localizer["ImportNewProfile"]),
            .. saved.Select(p => new ImportProfileOption(p, p.Name)),
        ];
    }

    private async Task EnterTargetAsync()
    {
        ImportProfileOption? match;
        _loading = true;
        try
        {
            if (PropertyChoices.Count == 0)
            {
                var properties = await _properties.ListAsync(false);
                PropertyChoices = [.. properties.Select(p => new ChoiceOption(p.Id, p.Name, string.Empty))];
            }

            ProfileChoices = await BuildProfileChoicesAsync();
            var sheetName = SelectedSheet!.Name;
            match = ProfileChoices.FirstOrDefault(p => p.Profile is { } s && string.Equals(s.Profile.SheetName, sheetName, StringComparison.OrdinalIgnoreCase));
            SelectedProfile = match ?? ProfileChoices[0];
            if (match?.Profile is { } saved)
            {
                ProfileName = saved.Name;
            }
            else if (string.IsNullOrWhiteSpace(ProfileName))
            {
                ProfileName = sheetName;
            }

            if (SelectedProperty is null && PropertyChoices.Count == 1)
            {
                SelectedProperty = PropertyChoices[0];
            }
        }
        finally
        {
            _loading = false;
        }

        if (match?.Profile is { } profile)
        {
            await ReselectTargetFromProfileAsync(profile);
        }
        else
        {
            await LoadContractsAsync(SelectedProperty, SelectedContract?.Id);
        }
    }

    private async Task ReselectTargetFromProfileAsync(SavedImportProfile saved)
    {
        var contract = saved.Profile.ContractId == Guid.Empty ? null : await _contracts.GetAsync(saved.Profile.ContractId);
        if (contract is null)
        {
            return;
        }

        _loading = true;
        try
        {
            SelectedProperty = PropertyChoices.FirstOrDefault(p => p.Id == contract.PropertyId);
        }
        finally
        {
            _loading = false;
        }

        await LoadContractsAsync(SelectedProperty, contract.Id);
    }

    private async Task LoadContractsAsync(ChoiceOption? property, Guid? selectContract)
    {
        _loading = true;
        try
        {
            if (property is null)
            {
                ContractChoices = [];
                SelectedContract = null;
            }
            else
            {
                var contracts = await _contracts.ListByPropertyAsync(property.Id);
                ContractChoices = [.. contracts.Select(c => new ChoiceOption(c.Id, c.Title, string.Empty))];
                SelectedContract = ContractChoices.FirstOrDefault(c => c.Id == selectContract)
                    ?? (ContractChoices.Count == 1 ? ContractChoices[0] : null);
            }

            TargetHintText = PropertyChoices.Count == 0
                ? _localizer["ImportNoProperties"]
                : property is not null && ContractChoices.Count == 0 ? _localizer["ImportNoContracts"] : string.Empty;
        }
        finally
        {
            _loading = false;
        }

        await LoadContractMetersAsync();
    }

    private async Task LoadContractMetersAsync()
    {
        _contract = null;
        _registerLabels.Clear();
        if (SelectedContract is not { } choice || SelectedProperty is not { } property)
        {
            MeterLines = [];
            return;
        }

        _contract = await _contracts.GetAsync(choice.Id);
        _propertyMeters = await _meters.ListByPropertyAsync(property.Id);
        _propertyLinks = await LoadPropertyLinksAsync(property.Id);
        var links = await _contracts.ListContractMetersAsync(choice.Id);
        var lines = new List<string>();

        // A position is a chain of meters that replaced one another; the readings of the whole chain go to one column.
        foreach (var first in links
            .Select(l => _propertyMeters.FirstOrDefault(m => m.Id == l.MeterId))
            .OfType<Meter>()
            .Select(m => MeterChain.Build(_propertyMeters, m.Id).Meters[0])
            .DistinctBy(m => m.Id))
        {
            foreach (var register in first.Registers.Where(r => !r.IsDeleted))
            {
                var label = RegisterDisplay(first, register);
                _registerLabels[register.Id] = label;
                lines.Add(label);
            }
        }

        MeterLines = lines;
    }

    private async Task<IReadOnlyList<ContractMeter>> LoadPropertyLinksAsync(Guid propertyId)
    {
        var links = new List<ContractMeter>();
        foreach (var contract in await _contracts.ListByPropertyAsync(propertyId))
        {
            links.AddRange(await _contracts.ListContractMetersAsync(contract.Id));
        }

        return links;
    }

    private string RegisterDisplay(Meter meter, MeterRegister register)
    {
        var parts = new List<string> { EnumText.Get(_localizer, meter.Resource) };
        var detail = !string.IsNullOrWhiteSpace(meter.Location) ? meter.Location : meter.SerialNumber;
        if (!string.IsNullOrWhiteSpace(detail))
        {
            parts.Add(detail!);
        }

        var kind = RegisterLabel.For(_localizer, meter, register);
        if (kind.Length > 0)
        {
            parts.Add(kind);
        }

        return string.Join(" · ", parts);
    }

    private async Task EnterMappingAsync()
    {
        var sheetName = SelectedSheet!.Name;
        if (_sheet is null || _sheet.Name != sheetName)
        {
            _sheet = await Task.Run(() =>
            {
                using var stream = new MemoryStream(_workbook, writable: false);
                return _reader.ReadSheet(stream, sheetName);
            });
        }

        var registerIds = _registerLabels.Keys.ToList();
        var saved = SelectedProfile?.Profile;
        ImportProfile profile;
        if (saved is not null)
        {
            profile = saved.Profile.Clone();
        }
        else
        {
            profile = ImportColumnGuesser.Guess(_sheet, 1, registerIds);
            if (_contract is not null)
            {
                var suggestion = ImportPeriodDefaults.For(_contract);
                profile.PeriodRule = suggestion.Rule;
                profile.DateIsPaymentDate = suggestion.DateIsPaymentDate;
            }
        }

        _replacementEdits = [];

        profile.SheetName = sheetName;
        profile.ContractId = _contract?.Id ?? Guid.Empty;
        _loadedProfile = profile;
        ApplyProfileToUi(profile, keepRows: false);
        BuildPreview();
    }

    private void ApplyProfileToUi(ImportProfile profile, bool keepRows)
    {
        _applying = true;
        try
        {
            ApplyProfileCore(profile, keepRows);
        }
        finally
        {
            _applying = false;
        }
    }

    private void ApplyProfileCore(ImportProfile profile, bool keepRows)
    {
        if (!keepRows)
        {
            HeaderRowText = profile.HeaderRow.ToString(CultureInfo.InvariantCulture);
            FirstRowText = profile.FirstDataRow.ToString(CultureInfo.InvariantCulture);
            LastRowText = profile.LastDataRow?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            PaidMarker = profile.PaidMarker;
        }
        else if (int.TryParse(HeaderRowText, out var header) && FirstRowText.Length > 0)
        {
            profile.HeaderRow = header;
        }

        SelectedCurrencyRule = CurrencyRules.FirstOrDefault(r => r.Value == profile.CurrencyRule) ?? CurrencyRules[0];
        SelectedPeriodRule = PeriodRules.FirstOrDefault(r => r.Value == profile.PeriodRule) ?? PeriodRules[0];
        PeriodDayLimitText = profile.PeriodDayLimit.ToString(CultureInfo.InvariantCulture);
        DateIsPaymentDate = profile.DateIsPaymentDate;
        DetectReplacements = profile.DetectReplacements;
        _rowOverrides = [.. profile.RowOverrides];
        RuleChanges = [.. profile.PeriodRuleChanges.OrderBy(c => c.FromDate).Select(NewRuleChangeRow)];
        RebuildColumnOptions(profile.HeaderRow, profile);
        _notesColumns = [.. profile.NotesColumns];
        UpdateNotesSummary();
        MappingError = string.Empty;
    }

    private void RebuildColumnOptions(int headerRow, ImportProfile? profile = null)
    {
        var sheet = _sheet!;
        var header = sheet.Row(headerRow);
        var none = new ImportColumnOption(0, _localizer["ImportColumnNone"]);
        var options = new List<ImportColumnOption> { none };
        for (var column = 1; column <= sheet.ColumnCount; column++)
        {
            var letter = SheetData.ColumnLetter(column);
            var text = header?.Cell(column).Text.Trim() ?? string.Empty;
            options.Add(new ImportColumnOption(column, text.Length == 0 ? letter : $"{letter} — {Truncate(text, 28)}"));
        }

        ColumnOptions = options;
        PreviewHeader = options.Skip(1).ToList();

        if (profile is null)
        {
            foreach (var picker in AllPickers())
            {
                picker.SetOptions(options);
            }

            return;
        }

        PeriodPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldPeriod"], options, profile.PeriodColumn);
        TotalPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldTotal"], options, profile.TotalColumn);
        TotalFallbackPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldTotalFallback"], options, profile.TotalFallbackColumn);
        NetPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldNet"], options, profile.NetColumn);
        NetFallbackPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldNetFallback"], options, profile.NetFallbackColumn);
        VatPercentPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldVatPercent"], options, profile.VatPercentColumn);
        VatPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldVat"], options, profile.VatColumn);
        VatFallbackPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldVatFallback"], options, profile.VatFallbackColumn);
        PaidPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldPaid"], options, profile.PaidColumn);
        PaidDatePicker = new ImportColumnPickerViewModel(_localizer["ImportFieldPaidDate"], options, profile.PaidDateColumn);
        QuantityPicker = new ImportColumnPickerViewModel(_localizer["ImportFieldQuantity"], options, profile.QuantityColumn);

        RegisterMappings =
        [
            .. _registerLabels.Select(pair =>
            {
                var mapping = profile.Registers.FirstOrDefault(r => r.RegisterId == pair.Key);
                return new ImportRegisterMappingViewModel(
                    pair.Key,
                    pair.Value,
                    new ImportColumnPickerViewModel(_localizer["ImportFieldReading"], options, mapping?.ReadingColumn ?? 0),
                    new ImportColumnPickerViewModel(_localizer["ImportFieldReadingDate"], options, mapping?.DateColumn ?? 0));
            }),
        ];
    }

    private IEnumerable<ImportColumnPickerViewModel> AllPickers() =>
        new[] { PeriodPicker, TotalPicker, TotalFallbackPicker, NetPicker, NetFallbackPicker, VatPercentPicker, VatPicker, VatFallbackPicker, PaidPicker, PaidDatePicker, QuantityPicker }
            .Concat(RegisterMappings.SelectMany(r => new[] { r.Reading, r.Date }));

    private void BuildPreview()
    {
        var sheet = _sheet!;
        TryParseRows(out var header, out var first, out _);
        var start = Math.Max(1, Math.Min(header, first));
        PreviewRows =
        [
            .. Enumerable.Range(start, PreviewRowCount)
                .Select(sheet.Row)
                .Where(r => r is not null)
                .Select(r => new ImportSheetPreviewRow(r!.Number.ToString(CultureInfo.InvariantCulture), [.. Enumerable.Range(1, sheet.ColumnCount).Select(c => Truncate(r.Cell(c).Text, 24))])),
        ];
    }

    private bool TryParseRows(out int header, out int first, out int? last)
    {
        last = null;
        first = 0;
        var ok = TryRow(HeaderRowText, true, out header) && TryRow(FirstRowText, false, out first);
        if (ok && LastRowText.Trim().Length > 0)
        {
            ok = TryRow(LastRowText, false, out var lastRow);
            last = lastRow;
        }

        if (!ok)
        {
            MappingError = ValidationText.For(_localizer, new ValidationError(ErrorCodes.ImportRowsInvalid));
        }

        return ok;

        static bool TryRow(string text, bool allowZero, out int row) =>
            int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out row) && row >= (allowZero ? 0 : 1) && row <= MaxRowNumber;
    }

    private bool TryBuildProfile(out ImportProfile profile)
    {
        profile = new ImportProfile();
        MappingError = string.Empty;
        if (!TryParseRows(out var header, out var first, out var last))
        {
            return false;
        }

        profile = new ImportProfile
        {
            SheetName = SelectedSheet?.Name ?? string.Empty,
            HeaderRow = header,
            FirstDataRow = first,
            LastDataRow = last,
            ContractId = _contract?.Id ?? Guid.Empty,
            PeriodColumn = PeriodPicker.Column,
            TotalColumn = TotalPicker.Column,
            TotalFallbackColumn = TotalFallbackPicker.Column,
            NetColumn = NetPicker.Column,
            NetFallbackColumn = NetFallbackPicker.Column,
            VatPercentColumn = VatPercentPicker.Column,
            VatColumn = VatPicker.Column,
            VatFallbackColumn = VatFallbackPicker.Column,
            PeriodRule = SelectedPeriodRule?.Value ?? ImportPeriodRule.MonthOfDate,
            PeriodDayLimit = int.TryParse(PeriodDayLimitText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var dayLimit) ? dayLimit : 0,
            PeriodRuleChanges = [.. RuleChanges.Select(r => r.ToChange()).OrderBy(c => c.FromDate)],
            DateIsPaymentDate = DateIsPaymentDate,
            DetectReplacements = DetectReplacements,
            RowOverrides = [.. _rowOverrides],
            PaidColumn = PaidPicker.Column,
            PaidMarker = PaidMarker.Trim(),
            PaidDateColumn = PaidDatePicker.Column,
            QuantityColumn = QuantityPicker.Column,
            NotesColumns = [.. _notesColumns],
            CurrencyRule = SelectedCurrencyRule?.Value ?? ImportCurrencyRule.LvlBefore2014,
            Registers =
            [
                .. RegisterMappings
                    .Where(r => r.Reading.Column > 0)
                    .Select(r => new RegisterColumnMapping { RegisterId = r.RegisterId, ReadingColumn = r.Reading.Column, DateColumn = r.Date.Column }),
            ],
        };

        var validation = ImportProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            MappingError = string.Join(Environment.NewLine, validation.Errors.Select(e => ValidationText.For(_localizer, e)));
            return false;
        }

        if (_sheet is not null)
        {
            ImportColumnHeaders.Record(profile, _sheet);
        }

        return true;
    }

    private async Task EnterPreviewAsync(ImportProfile profile)
    {
        var sheet = _sheet!;
        var contract = _contract!;
        var bills = await _bills.ListByContractAsync(contract.Id);
        var plans = await _plans.ListByContractAsync(contract.Id);
        var context = new ImportContext(contract, _propertyMeters, bills, _propertyLinks, _replacementEdits, plans);
        var plan = await Task.Run(() => ImportPlanner.Plan(profile, sheet, context));
        _plan = plan;
        _plannedProfile = profile;

        PlanProblemText = plan.Problems.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, plan.Problems.Select(p => ValidationText.For(_localizer, p)));
        _allPlanRows = [.. plan.Rows.Select(r => new ImportPlanRowViewModel(r, _localizer, _registerLabels, EditRowPeriodAsync))];
        ReplacementRows =
        [
            .. plan.Replacements.SelectMany(replacement => replacement.Registers
                .Where(line => !line.Unmapped)
                .Select(line => new ImportReplacementRowViewModel(replacement, line, _registerLabels.GetValueOrDefault(line.PositionRegisterId, string.Empty), _localizer))),
        ];
        var counts = plan.Counts;
        SummaryText = plan.Problems.Count > 0
            ? string.Empty
            : _localizer.Get("ImportSummary", counts.Readings, counts.Bills, counts.Payments, counts.SkippedRows, plan.ErrorRows, plan.WarningRows, counts.Replacements);
        CanImport = plan.Problems.Count == 0 && (counts.Readings + counts.Bills) > 0;
        if (plan is { RowsBeforeContractStart: > 0, SuggestedContractStart: { } suggested, ContractValidFrom: { } validFrom })
        {
            BeforeStartText = _localizer.Get("ImportBeforeStart", plan.RowsBeforeContractStart, validFrom.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            SetContractStartText = _localizer.Get("ImportSetContractStart", suggested.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }
        else
        {
            BeforeStartText = string.Empty;
            SetContractStartText = string.Empty;
        }

        // Charges and reports count a meter only inside its link, so a link that starts after the imported months hides them.
        _linkMoves = ImportLinkStarts.Find(plan, contract, _propertyMeters, _propertyLinks);
        if (_linkMoves.Count > 0 && ImportLinkStarts.EarliestImportedDate(plan) is { } earliest)
        {
            LinkStartText = _localizer.Get("ImportLinkStartLate", _linkMoves.Count, earliest.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            if (SetContractStartText.Length == 0)
            {
                SetContractStartText = _localizer["ImportMoveLinkStarts"];
            }
        }
        else
        {
            LinkStartText = string.Empty;
        }

        SelectedFilter = Filters[0];
        ApplyFilter();
    }

    private ImportRuleChangeRowViewModel NewRuleChangeRow(PeriodRuleChange change) =>
        new(change, PeriodRules, _localizer["ImportRuleChangeFrom"], _localizer["ImportDayLimitLabel"], _localizer["ImportRemove"], RemoveRuleChange);

    private void RemoveRuleChange(ImportRuleChangeRowViewModel row) => RuleChanges = [.. RuleChanges.Where(r => r != row)];

    // The guessed columns replace the mapping, not the owner's choices about the period.
    private void CopyPeriodSettings(ImportProfile target)
    {
        target.PeriodRule = SelectedPeriodRule?.Value ?? target.PeriodRule;
        target.PeriodDayLimit = int.TryParse(PeriodDayLimitText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var limit) ? limit : target.PeriodDayLimit;
        target.PeriodRuleChanges = [.. RuleChanges.Select(r => r.ToChange())];
        target.DateIsPaymentDate = DateIsPaymentDate;
        target.DetectReplacements = DetectReplacements;
        target.RowOverrides = [.. _rowOverrides];
    }

    /// <summary>Adds a line "from this date on use that rule" (it starts as a copy of the base rule, from the first of the current year).</summary>
    [RelayCommand]
    private void AddRuleChange()
    {
        var change = new PeriodRuleChange
        {
            FromDate = new DateOnly(DateTime.Today.Year, 1, 1),
            Rule = SelectedPeriodRule?.Value ?? ImportPeriodRule.MonthOfDate,
        };
        RuleChanges = [.. RuleChanges, NewRuleChangeRow(change)];
    }

    /// <summary>
    /// Moves the contract's "valid from" to the first day of the earliest imported period and the start of the meter links of the
    /// contract to the installation of their meters (after a confirmation), and plans the import again, so that charges and reports
    /// include the imported history.
    /// </summary>
    [RelayCommand]
    private Task SetContractStartAsync() => RunSafeAsync(async () =>
    {
        if (_plan is null || _contract is null || !TryBuildProfile(out var profile))
        {
            return;
        }

        var start = _plan.SuggestedContractStart;
        var moves = _linkMoves;
        if (start is null && moves.Count == 0)
        {
            return;
        }

        var message = start is { } newStart
            ? _localizer.Get(
                "ImportSetContractStartConfirm",
                _contract.ValidFrom.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                newStart.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
            : string.Empty;
        if (moves.Count > 0)
        {
            var links = _localizer.Get("ImportSetStartLinksConfirm", moves.Count);
            message = message.Length == 0 ? links : message + Environment.NewLine + Environment.NewLine + links;
        }

        var confirmed = await _dialogs.ConfirmAsync(_localizer["ImportTitle"], message, _localizer["Save"], _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (start is { } contractStart)
            {
                _contract.ValidFrom = contractStart;
                await _contracts.SaveAsync(_contract);
            }

            foreach (var move in moves)
            {
                move.Link.ValidFrom = move.NewValidFrom;
                await _contracts.SaveContractMeterAsync(move.Link);
            }

            if (moves.Count > 0 && SelectedProperty is { } property)
            {
                _propertyLinks = await LoadPropertyLinksAsync(property.Id);
            }

            await EnterPreviewAsync(profile);
        }
        finally
        {
            IsBusy = false;
        }
    });

    /// <summary>Plans the import again with the values the owner corrected in the list of meter replacements.</summary>
    [RelayCommand]
    private Task RecalculateAsync() => RunSafeAsync(async () =>
    {
        if (!TryBuildProfile(out var profile))
        {
            return;
        }

        _replacementEdits = [.. ReplacementRows.Select(r => r.ToEdit()).OfType<ReplacementEdit>()];
        IsBusy = true;
        try
        {
            await EnterPreviewAsync(profile);
        }
        finally
        {
            IsBusy = false;
        }
    });

    // Opens the dialog for the period of one row; the correction is kept in the profile by the date of the row.
    private Task EditRowPeriodAsync(PlannedRow row) => RunSafeAsync(async () =>
    {
        if (row.RowDate is not { } rowDate)
        {
            return;
        }

        var editor = new ImportRowOverrideEditorViewModel(_localizer, row);
        await _dialogs.ShowEditorAsync(editor);
        if (!editor.Saved)
        {
            return;
        }

        _rowOverrides = [.. _rowOverrides.Where(o => o.RowDate != rowDate)];
        if (editor.Result is { } result)
        {
            _rowOverrides.Add(result);
        }

        if (!TryBuildProfile(out var profile))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await EnterPreviewAsync(profile);
        }
        finally
        {
            IsBusy = false;
        }
    });

    private void ApplyFilter()
    {
        var filter = SelectedFilter?.Value ?? ImportRowFilter.All;
        PlanRows =
        [
            .. _allPlanRows.Where(r => filter switch
            {
                ImportRowFilter.Errors => r.HasErrors,
                ImportRowFilter.Warnings => r.HasWarnings,
                _ => true,
            }),
        ];
    }

    private async Task<SavedImportProfile> SaveProfileAsync(ImportProfile profile)
    {
        var name = ProfileName.Trim();
        if (name.Length == 0)
        {
            name = profile.SheetName;
        }

        name = name.Length > InputLimits.Name ? name[..InputLimits.Name] : name;
        var saved = SelectedProfile?.Profile ?? new SavedImportProfile();
        saved.Name = name;
        saved.Profile = profile;
        await _profiles.SaveAsync(saved);
        return saved;
    }

    private void UpdateNotesSummary()
    {
        NotesSummary = _notesColumns.Count == 0
            ? _localizer["ImportColumnNone"]
            : string.Join(", ", _notesColumns.Select(c => ColumnOptions.FirstOrDefault(o => o.Index == c)?.Text ?? SheetData.ColumnLetter(c)));
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";

    private static Guid ColumnToId(int column) => new(column, 0, 0, new byte[8]);

    private static int IdToColumn(Guid id) => BitConverter.ToInt32(id.ToByteArray(), 0);
}
