using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Reports;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The Reports screen: costs, consumption, unit cost and calculated vs billed as tables over a period, with filters and an
/// export to Excel and CSV. The data is loaded once when the screen appears; every change of a filter recalculates the table
/// from it. A tap on a number opens the bills or readings behind it. The same numbers can be shown as charts (instead of the table
/// on narrow screens, above it on wide ones).
/// </summary>
public sealed partial class ReportsViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    /// <summary>Content type of an .xlsx file.</summary>
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IReportDataLoader _loader;
    private readonly IReportWorkbookWriter _excel;
    private readonly IFileSaveService _files;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _snackbar;
    private readonly ReportFilterState _state;
    private readonly ILayoutModeService _layout;
    private readonly IDialogService _dialogs;
    private int _busyCount;
    private readonly SemaphoreSlim _calculation = new(1, 1);

    private ReportData? _data;
    private ReportTable? _table;
    private bool _hasData;
    private int _version;
    private Task _last = Task.CompletedTask;

    [ObservableProperty]
    private ReportTableViewModel _tableView = ReportTableViewModel.Empty;

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _kinds = [];

    [ObservableProperty]
    private IReadOnlyList<MultiSelectItemViewModel> _propertyChoices = [];

    [ObservableProperty]
    private IReadOnlyList<MultiSelectItemViewModel> _categoryChoices = [];

    [ObservableProperty]
    private bool _filtersExpanded;

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _groupings = [];

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _measures = [];

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _rowModes = [];

    [ObservableProperty]
    private string _fromText = string.Empty;

    [ObservableProperty]
    private string _toText = string.Empty;

    [ObservableProperty]
    private string _periodFieldText = string.Empty;

    [ObservableProperty]
    private string _propertiesFieldText = string.Empty;

    [ObservableProperty]
    private string _categoriesFieldText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _legendText = string.Empty;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private IReadOnlyList<ChartModel> _charts = [];

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _viewModes = [];

    [ObservableProperty]
    private bool _showLegend;

    [ObservableProperty]
    private bool _yearOverYear;

    [ObservableProperty]
    private bool _perMeter;

    public ReportsViewModel(
        IReportDataLoader loader,
        IReportWorkbookWriter excel,
        IFileSaveService files,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ISnackbarService snackbar,
        ReportFilterState state,
        ILayoutModeService layout,
        IDialogService dialogs)
    {
        _loader = loader;
        _excel = excel;
        _files = files;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _snackbar = snackbar;
        _state = state;
        _layout = layout;
        _dialogs = dialogs;
        _filtersExpanded = state.FiltersExpanded ?? layout.Current != LayoutMode.Compact;
        _yearOverYear = state.YearOverYear;
        _perMeter = state.PerMeter;
        RefreshTexts();
    }

    public string PeriodLabel => _localizer["PeriodLabel"];

    /// <summary>Columns of the filter field grid: 3 on expanded, 2 on medium, 1 on compact layouts.</summary>
    public int FilterColumns => _layout.Current switch
    {
        LayoutMode.Expanded => 3,
        LayoutMode.Medium => 2,
        _ => 1,
    };

    public bool ShowFilterSummary => !FiltersExpanded;

    public string FiltersToggleText => _localizer[FiltersExpanded ? "ReportFiltersCollapse" : "ReportFiltersExpand"];

    public string FiltersTitle => _localizer["ReportFiltersTitle"];

    public string PropertiesLabel => _localizer["ReportPropertiesLabel"];

    public string CategoriesLabel => _localizer["ReportCategoriesLabel"];

    public string GroupingLabel => _localizer["ReportGroupingLabel"];

    public string MeasureLabel => _localizer["ReportMeasureLabel"];

    public string RowsLabel => _localizer["ReportRowsLabel"];

    public string YearOverYearText => _localizer["ReportYearOverYear"];

    public string PerMeterText => _localizer["ReportPerMeter"];

    public string ExportExcelText => _localizer["ReportExportExcel"];

    public string ExportCsvText => _localizer["ReportExportCsv"];

    public ReportKind Kind => _state.Kind;

    public bool ShowCategories => Kind == ReportKind.Costs && CategoryChoices.Count > 1;

    public bool ShowGrouping => Kind == ReportKind.Costs;

    public bool ShowMeasure => Kind != ReportKind.Consumption;

    public bool ShowRowModes => !(YearOverYear && ShowYearOverYear);

    public bool ShowYearOverYear => Kind is ReportKind.Costs or ReportKind.Consumption;

    public bool ShowPerMeter => Kind == ReportKind.Consumption;

    /// <summary>True on narrow screens, where the owner picks the table or the chart; wide screens show both.</summary>
    public bool ShowViewSwitch => _hasData && _layout.Current != LayoutMode.Expanded;

    public bool ShowTable => _hasData && (_layout.Current == LayoutMode.Expanded || !_state.ShowChart || Charts.Count == 0);

    public bool ShowChart => _hasData && Charts.Count > 0 && (_layout.Current == LayoutMode.Expanded || _state.ShowChart);

    public string ViewLabel => _localizer["ReportViewLabel"];

    public string ChartHintText => _localizer["ReportChartHint"];

    public bool CanExport => _table is { IsEmpty: false };

    /// <summary>Completes when the recalculation started by the last button tap or language change has finished.</summary>
    public Task WhenIdleAsync() => _last;

    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();
        await RunSafeAsync(ReloadAsync);
    }

    /// <summary>Loads the data again (the screen appears, or the owner changed something elsewhere) and recalculates the table.</summary>
    public async Task ReloadAsync()
    {
        // A counter, not a flag: two overlapping loads (appearing + language change) must not leave the spinner on.
        _busyCount++;
        IsBusy = true;
        try
        {
            _data = await _loader.LoadAsync();
            BuildChoices();
            await RebuildAsync();
        }
        finally
        {
            if (--_busyCount <= 0)
            {
                _busyCount = 0;
                IsBusy = false;
            }
        }
    }

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionReports"];
        HelpText = _localizer["SectionHelp"];
        EmptyText = _localizer["ReportEmpty"];
        LegendText = _localizer["ReportLegend"];
        BuildOptions();
        OnPropertyChanged(string.Empty);
        if (_data is not null)
        {
            BuildChoices();
            _last = RunSafeAsync(RebuildAsync);
        }
    }

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message)
    {
        if (_state.FiltersExpanded is null)
        {
            FiltersExpanded = _layout.Current != LayoutMode.Compact;
        }

        OnPropertyChanged(string.Empty);
    }

    // ---- Filters --------------------------------------------------------------------------------------------------

    [RelayCommand]
    private void ToggleFilters()
    {
        FiltersExpanded = !FiltersExpanded;
    }

    partial void OnFiltersExpandedChanged(bool value)
    {
        _state.FiltersExpanded = value;
        OnPropertyChanged(nameof(ShowFilterSummary));
        OnPropertyChanged(nameof(FiltersToggleText));
    }

    [RelayCommand]
    private Task ChoosePeriodAsync() => RunSafeAsync(async () =>
    {
        if (_data is null)
        {
            return;
        }

        var (from, to) = CurrentRange();
        var dialog = new ReportPeriodDialogViewModel(
            _localizer,
            _state.Preset,
            from,
            to,
            _data.EarliestMonth ?? from,
            _data.LatestMonth ?? to);
        if (!await _dialogs.ShowEditorAsync(dialog))
        {
            return;
        }

        _state.Preset = dialog.Preset;
        if (dialog.Preset == ReportPreset.Custom)
        {
            (_state.From, _state.To) = dialog.Range;
        }

        await RebuildAsync();
    });

    [RelayCommand]
    private Task ChoosePropertiesAsync() => RunSafeAsync(async () =>
    {
        if (_data is null)
        {
            return;
        }

        var dialog = new MultiSelectDialogViewModel(_localizer, PropertiesLabel, Copy(PropertyChoices), SelectedPropertyIds());
        if (!await _dialogs.ShowEditorAsync(dialog))
        {
            return;
        }

        _state.Properties = dialog.SelectedIds;
        BuildChoices();
        await RebuildAsync();
    });

    [RelayCommand]
    private Task ChooseCategoriesAsync() => RunSafeAsync(async () =>
    {
        if (_data is null)
        {
            return;
        }

        var all = _data.Categories.Select(c => c.Id).ToHashSet();
        var dialog = new MultiSelectDialogViewModel(_localizer, CategoriesLabel, Copy(CategoryChoices), _state.Categories ?? all);
        if (!await _dialogs.ShowEditorAsync(dialog))
        {
            return;
        }

        var selected = dialog.SelectedIds;
        _state.Categories = selected.SetEquals(all) ? null : selected;
        BuildChoices();
        await RebuildAsync();
    });

    // The dialog works on copies: Cancel must leave the screen's choices untouched.
    private static IEnumerable<MultiSelectItemViewModel> Copy(IEnumerable<MultiSelectItemViewModel> items) =>
        items.Select(i => new MultiSelectItemViewModel(i.Id, i.Text, i.Color, i.IsSelected));

    /// <summary>The index of the chosen "group by" option (bound to a picker).</summary>
    public int GroupingIndex
    {
        get => IndexOf(Groupings);
        set => ChooseAt(Groupings, value);
    }

    /// <summary>The index of the chosen "show" option (bound to a picker).</summary>
    public int MeasureIndex
    {
        get => IndexOf(Measures);
        set => ChooseAt(Measures, value);
    }

    /// <summary>The index of the chosen "rows" option (bound to a picker).</summary>
    public int RowModeIndex
    {
        get => IndexOf(RowModes);
        set => ChooseAt(RowModes, value);
    }

    private static int IndexOf(IReadOnlyList<ReportOptionViewModel> options)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].IsSelected)
            {
                return i;
            }
        }

        return -1;
    }

    // A picker reports -1 while its items are replaced, and the index it already shows when it is bound: ignore both.
    private static void ChooseAt(IReadOnlyList<ReportOptionViewModel> options, int index)
    {
        if (index >= 0 && index < options.Count && !options[index].IsSelected)
        {
            options[index].ChooseCommand.Execute(null);
        }
    }

    partial void OnYearOverYearChanged(bool value)
    {
        _state.YearOverYear = value;
        OnPropertyChanged(nameof(ShowRowModes));
        if (_data is not null)
        {
            _last = RunSafeAsync(RebuildAsync);
        }
    }

    partial void OnPerMeterChanged(bool value)
    {
        _state.PerMeter = value;
        if (_data is not null)
        {
            _last = RunSafeAsync(RebuildAsync);
        }
    }

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, HelpTopic.Reports));

    private void Choose(ReportKind kind) => _last = RunSafeAsync(async () =>
    {
        _state.Kind = kind;
        await RebuildAsync();
    });

    private void ChooseView(bool chart)
    {
        _state.ShowChart = chart;
        foreach (var mode in ViewModes)
        {
            mode.IsSelected = (bool)mode.Value! == chart;
        }

        OnPropertyChanged(string.Empty);
    }

    private void Choose(ReportPreset preset) => _last = RunSafeAsync(async () =>
    {
        _state.Preset = preset;
        await RebuildAsync();
    });

    private void Choose(CostGrouping grouping) => _last = RunSafeAsync(async () =>
    {
        _state.Grouping = grouping;
        await RebuildAsync();
    });

    private void Choose(ReportMeasure measure) => _last = RunSafeAsync(async () =>
    {
        if (Kind == ReportKind.CalculatedVsBilled)
        {
            _state.CompareMeasure = measure;
        }
        else
        {
            _state.CostMeasure = measure;
        }

        await RebuildAsync();
    });

    private void Choose(ReportRows rows) => _last = RunSafeAsync(async () =>
    {
        _state.Rows = rows;
        await RebuildAsync();
    });

    // ---- Building the table ---------------------------------------------------------------------------------------

    private HashSet<Guid> SelectedPropertyIds() => _state.Properties is { } chosen
        ? [.. chosen.Where(id => _data!.Properties.Any(p => p.Id == id))]
        : [.. _data!.Properties.Where(p => !p.IsArchived).Select(p => p.Id)];

    private (YearMonth From, YearMonth To) CurrentRange()
    {
        if (_state is { Preset: ReportPreset.Custom, From: { } from, To: { } to })
        {
            return (from, to);
        }

        return ReportPeriods.Resolve(
            _state.Preset == ReportPreset.Custom ? ReportPreset.ThisYear : _state.Preset,
            _clock.Today,
            _data?.EarliestMonth,
            _data?.LatestMonth);
    }

    private ReportFilter CurrentFilter()
    {
        var (from, to) = CurrentRange();
        var measure = Kind == ReportKind.CalculatedVsBilled ? _state.CompareMeasure : _state.CostMeasure;
        return new ReportFilter(
            from,
            to,
            [.. SelectedPropertyIds()],
            Kind == ReportKind.Costs ? _state.Categories : null,
            _state.Rows,
            YearOverYear && ShowYearOverYear,
            _state.Grouping,
            measure,
            PerMeter);
    }

    private async Task RebuildAsync()
    {
        if (_data is null)
        {
            return;
        }

        var data = _data;
        var filter = CurrentFilter();
        var kind = Kind;
        var version = Interlocked.Increment(ref _version);

        // ReportData remembers calculated charges and is not thread-safe: one calculation at a time.
        ReportTable table;
        await _calculation.WaitAsync();
        try
        {
            if (version != _version)
            {
                return;
            }

            table = await Task.Run(() => ReportBuilder.Build(kind, data, filter));
        }
        finally
        {
            _calculation.Release();
        }

        if (version != _version)
        {
            return;
        }

        _table = table;
        var labels = new ReportLabels(_localizer);
        TableView = ReportTableViewModel.From(table, labels, OpenDrill);
        _hasData = !table.IsEmpty;
        Charts = ReportChartBuilder.Build(table, labels, _localizer, data.Categories.ToDictionary(c => c.Id, c => c.Color));
        ShowEmpty = table.IsEmpty;
        ShowLegend = !table.IsEmpty;
        var (from, to) = (filter.From, filter.To);
        FromText = BillText.Period(from);
        ToText = BillText.Period(to);
        BuildOptions();
        BuildChoices();
        BuildFieldTexts(from, to);
        OnPropertyChanged(string.Empty);
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    private void BuildOptions()
    {
        var labels = new ReportLabels(_localizer);
        Kinds = [.. Enum.GetValues<ReportKind>().Select(k => new ReportOptionViewModel(labels.KindName(k), k == Kind, o => Choose((ReportKind)o.Value!)) { Value = k })];

        ViewModes =
        [
            new ReportOptionViewModel(_localizer["ReportViewTable"], !_state.ShowChart, o => ChooseView((bool)o.Value!)) { Value = false },
            new ReportOptionViewModel(_localizer["ReportViewChart"], _state.ShowChart, o => ChooseView((bool)o.Value!)) { Value = true },
        ];

        Groupings =
        [
            .. Enum.GetValues<CostGrouping>()
                .Select(g => new ReportOptionViewModel(_localizer["ReportGroup" + g], g == _state.Grouping, o => Choose((CostGrouping)o.Value!)) { Value = g }),
        ];

        ReportMeasure[] measures = Kind switch
        {
            ReportKind.Costs => [ReportMeasure.Gross, ReportMeasure.Net, ReportMeasure.Vat],
            ReportKind.UnitCost => [ReportMeasure.Gross, ReportMeasure.Net],
            ReportKind.CalculatedVsBilled =>
            [
                ReportMeasure.Difference, ReportMeasure.DifferencePercent, ReportMeasure.Calculated, ReportMeasure.Billed, ReportMeasure.QuantityDifference,
            ],
            _ => [],
        };
        var shown = Kind == ReportKind.CalculatedVsBilled ? _state.CompareMeasure : _state.CostMeasure;
        Measures = [.. measures.Select(m => new ReportOptionViewModel(labels.MeasureName(m), m == shown, o => Choose((ReportMeasure)o.Value!)) { Value = m })];

        RowModes =
        [
            new ReportOptionViewModel(_localizer["ReportRowsMonths"], _state.Rows == ReportRows.Months, o => Choose((ReportRows)o.Value!)) { Value = ReportRows.Months },
            new ReportOptionViewModel(_localizer["ReportRowsYears"], _state.Rows == ReportRows.Years, o => Choose((ReportRows)o.Value!)) { Value = ReportRows.Years },
        ];
    }

    private void BuildChoices()
    {
        if (_data is null)
        {
            return;
        }

        var selected = SelectedPropertyIds();
        PropertyChoices =
        [
            .. _data.Properties.OrderBy(p => p.IsArchived).ThenBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new MultiSelectItemViewModel(p.Id, p.Name, string.Empty, selected.Contains(p.Id))),
        ];

        var categories = _state.Categories;
        var labels = new ReportLabels(_localizer);
        CategoryChoices =
        [
            .. _data.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new MultiSelectItemViewModel(
                    c.Id,
                    c.Name ?? (c.SystemKey is { } key ? labels.ServiceName(key) : "—"),
                    c.Color,
                    categories is null || categories.Contains(c.Id))),
        ];
    }

    /// <summary>"All (3)", the names when they are few, else "4 of 15".</summary>
    public static string SummarizeSelection(ILocalizer localizer, IReadOnlyList<MultiSelectItemViewModel> choices)
    {
        var selected = choices.Where(c => c.IsSelected).Select(c => c.Text).ToList();
        if (selected.Count == choices.Count)
        {
            return localizer.Get("ReportFieldAll", choices.Count);
        }

        var names = string.Join(", ", selected);
        return selected.Count <= 2 && names.Length <= 32 ? names : localizer.Get("ReportFieldSome", selected.Count, choices.Count);
    }

    private void BuildFieldTexts(YearMonth from, YearMonth to)
    {
        var range = $"{BillText.Period(from)}–{BillText.Period(to)}";
        var presetName = _state.Preset == ReportPreset.Custom ? null : _localizer["ReportPreset" + _state.Preset];
        PeriodFieldText = presetName is null ? range : $"{presetName} ({range})";
        PropertiesFieldText = SummarizeSelection(_localizer, PropertyChoices);
        CategoriesFieldText = SummarizeSelection(_localizer, CategoryChoices);

        var parts = new List<string>
        {
            presetName ?? range,
            PropertyChoices.All(c => c.IsSelected) ? _localizer["ReportAllProperties"] : PropertiesFieldText,
        };
        if (ShowCategories)
        {
            parts.Add(CategoryChoices.All(c => c.IsSelected) ? _localizer["ReportAllCategories"] : CategoriesFieldText);
        }

        if (ShowGrouping && Groupings.FirstOrDefault(g => g.IsSelected) is { } grouping)
        {
            parts.Add(grouping.Text);
        }

        if (ShowMeasure && Measures.FirstOrDefault(m => m.IsSelected) is { } measure)
        {
            parts.Add(measure.Text);
        }

        SummaryText = string.Join(" · ", parts);
    }

    // ---- Opening what is behind a number ---------------------------------------------------------------------------

    private void OpenDrill(ReportDrill drill) => _ = RunSafeAsync(() => drill.Target == DrillTarget.Readings
        ? _navigation.GoToAsync(
            Routes.ReadingsFor,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = drill.PropertyId,
                [ReadingsViewModel.DateParameter] = drill.To.LastDay,
            })
        : _navigation.GoToAsync(Routes.BillsFor, BillsParameters(drill)));

    /// <summary>The query of the Bills screen for a cell: its property, the month or year it covers (else all years), the contract or category highlighted.</summary>
    public static Dictionary<string, object> BillsParameters(ReportDrill drill)
    {
        ArgumentNullException.ThrowIfNull(drill);

        var parameters = new Dictionary<string, object> { [EditorNavigation.PropertyIdParameter] = drill.PropertyId };
        if (drill.From == drill.To)
        {
            parameters[BillsViewModel.MonthParameter] = drill.From.ToString();
        }
        else if (drill.From.Year == drill.To.Year && drill.From.Month == 1 && drill.To.Month == 12)
        {
            parameters[BillsViewModel.YearParameter] = drill.From.Year;
        }

        if (drill.ContractId is { } contractId)
        {
            parameters[EditorNavigation.ContractIdParameter] = contractId;
        }

        if (drill.CategoryId is { } categoryId)
        {
            parameters[BillsViewModel.CategoryParameter] = categoryId;
        }

        return parameters;
    }

    // ---- Export ---------------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportExcelAsync() => ExportAsync("xlsx", ExcelContentType, (sheet, stream) => _excel.Write(sheet, stream));

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCsvAsync() => ExportAsync("csv", "text/csv", (sheet, stream) => ReportCsvWriter.Write(sheet, stream, CultureInfo.CurrentCulture));

    private Task ExportAsync(string extension, string contentType, Action<ReportSheet, Stream> write) => RunSafeAsync(async () =>
    {
        if (_table is not { IsEmpty: false } table || _data is null)
        {
            return;
        }

        var sheet = BuildSheet(table);
        var fileName = ReportSheetBuilder.FileName(table.Kind, table.Filter, extension);
        var result = await _files.SaveAsync(fileName, contentType, "Reports", stream =>
        {
            write(sheet, stream);
            return Task.CompletedTask;
        });
        if (!result.Saved)
        {
            return;
        }

        _snackbar.Notify(result.Location is { } location ? _localizer.Get("ReportSavedTo", location) : _localizer["ReportSaved"]);
    });

    private ReportSheet BuildSheet(ReportTable table)
    {
        var data = _data!;
        var selected = SelectedPropertyIds();
        var properties = data.Properties.Where(p => selected.Contains(p.Id)).Select(p => p.Name).ToList();
        var propertiesText = properties.Count == data.Properties.Count || properties.Count == 0
            ? _localizer["ReportAllProperties"]
            : string.Join(", ", properties);

        string? categoriesText = null;
        if (table.Kind == ReportKind.Costs)
        {
            var labels = new ReportLabels(_localizer);
            categoriesText = _state.Categories is { } chosen
                ? string.Join(", ", data.Categories.Where(c => chosen.Contains(c.Id)).Select(c => c.Name ?? (c.SystemKey is { } key ? labels.ServiceName(key) : "—")))
                : _localizer["ReportAllCategories"];
        }

        return ReportSheetBuilder.Build(table, new ReportLabels(_localizer), _localizer, propertiesText, categoriesText, _clock.Today);
    }
}
