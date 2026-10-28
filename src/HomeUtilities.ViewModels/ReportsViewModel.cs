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
    private IReadOnlyList<ReportOptionViewModel> _presets = [];

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _propertyChoices = [];

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _categoryChoices = [];

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
        ILayoutModeService layout)
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
        _yearOverYear = state.YearOverYear;
        _perMeter = state.PerMeter;
        RefreshTexts();
    }

    public string FromLabel => _localizer["ReportFromLabel"];

    public string ToLabel => _localizer["ReportToLabel"];

    public string PeriodLabel => _localizer["PeriodLabel"];

    public string PreviousMonthText => _localizer["PeriodPreviousMonth"];

    public string NextMonthText => _localizer["PeriodNextMonth"];

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
        IsBusy = true;
        try
        {
            _data = await _loader.LoadAsync();
            BuildChoices();
            await RebuildAsync();
        }
        finally
        {
            IsBusy = false;
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

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) => OnPropertyChanged(string.Empty);

    // ---- Filters --------------------------------------------------------------------------------------------------

    [RelayCommand]
    private Task PreviousFromAsync() => ShiftAsync(from: true, -1);

    [RelayCommand]
    private Task NextFromAsync() => ShiftAsync(from: true, 1);

    [RelayCommand]
    private Task PreviousToAsync() => ShiftAsync(from: false, -1);

    [RelayCommand]
    private Task NextToAsync() => ShiftAsync(from: false, 1);

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

    private Task ShiftAsync(bool from, int months) => RunSafeAsync(async () =>
    {
        var (start, end) = CurrentRange();
        if (from)
        {
            start = start.AddMonths(months);
            if (start > end)
            {
                end = start;
            }
        }
        else
        {
            end = end.AddMonths(months);
            if (end < start)
            {
                start = end;
            }
        }

        _state.Preset = ReportPreset.Custom;
        _state.From = start;
        _state.To = end;
        await RebuildAsync();
    });

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

    private void ToggleProperty(ReportOptionViewModel option) => _last = RunSafeAsync(async () =>
    {
        var id = (Guid)option.Value!;
        var selected = SelectedPropertyIds();
        if (!selected.Remove(id))
        {
            selected.Add(id);
        }

        // At least one property stays selected.
        if (selected.Count == 0)
        {
            selected.Add(id);
        }

        _state.Properties = selected;
        foreach (var choice in PropertyChoices)
        {
            choice.IsSelected = selected.Contains((Guid)choice.Value!);
        }

        await RebuildAsync();
    });

    private void ToggleCategory(ReportOptionViewModel option) => _last = RunSafeAsync(async () =>
    {
        var id = (Guid)option.Value!;
        var all = _data!.Categories.Select(c => c.Id).ToHashSet();
        HashSet<Guid> selected = [.. _state.Categories ?? all];
        if (!selected.Remove(id))
        {
            selected.Add(id);
        }

        if (selected.Count == 0)
        {
            selected.Add(id);
        }

        _state.Categories = selected.SetEquals(all) ? null : selected;
        foreach (var choice in CategoryChoices)
        {
            choice.IsSelected = selected.Contains((Guid)choice.Value!);
        }

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
        OnPropertyChanged(string.Empty);
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    private void BuildOptions()
    {
        var labels = new ReportLabels(_localizer);
        Kinds = [.. Enum.GetValues<ReportKind>().Select(k => new ReportOptionViewModel(labels.KindName(k), k == Kind, o => Choose((ReportKind)o.Value!)) { Value = k })];

        var current = _state.Preset;
        Presets =
        [
            .. new[] { ReportPreset.ThisYear, ReportPreset.Last12Months, ReportPreset.LastYear, ReportPreset.All }
                .Select(p => new ReportOptionViewModel(_localizer["ReportPreset" + p], p == current, o => Choose((ReportPreset)o.Value!)) { Value = p }),
        ];

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
                .Select(p => new ReportOptionViewModel(p.Name, selected.Contains(p.Id), ToggleProperty) { Value = p.Id }),
        ];

        var categories = _state.Categories;
        var labels = new ReportLabels(_localizer);
        CategoryChoices =
        [
            .. _data.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ReportOptionViewModel(
                    c.Name ?? (c.SystemKey is { } key ? labels.ServiceName(key) : "—"),
                    categories is null || categories.Contains(c.Id),
                    ToggleCategory) { Value = c.Id }),
        ];
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
