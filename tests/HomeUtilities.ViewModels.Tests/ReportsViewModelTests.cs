using System.Globalization;
using System.Text;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The Reports screen. Today is 27 Oct 2026; the flat has water bills 20.00 (Jan), 30.00 (Feb) and the cottage a power bill 40.00 (Feb).</summary>
public class ReportsViewModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly ContractTestKit _kit = new(Today);
    private readonly Property _flat = new() { Name = "Flat", SortOrder = 1 };
    private readonly Property _cottage = new() { Name = "Cottage", SortOrder = 2 };
    private readonly Category _water = new() { SystemKey = ServiceKind.ColdWater, SortOrder = 1 };
    private readonly Category _power = new() { SystemKey = ServiceKind.Electricity, SortOrder = 2 };
    private readonly FakeReportLoader _loader = new();
    private readonly FakeWorkbookWriter _excel = new();
    private readonly FakeFileSaveService _files = new();
    private readonly ReportFilterState _state = new();

    public ReportsViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var water = new Contract { PropertyId = _flat.Id, Title = "Water supply", CategoryId = _water.Id, ValidFrom = new DateOnly(2020, 1, 1) };
        var power = new Contract { PropertyId = _cottage.Id, Title = "Power", CategoryId = _power.Id, ValidFrom = new DateOnly(2020, 1, 1) };
        var bills = new List<Bill>
        {
            NewBill(water, 20m, new YearMonth(2026, 1)),
            NewBill(water, 30m, new YearMonth(2026, 2)),
            NewBill(power, 40m, new YearMonth(2026, 2)),
        };
        _loader.Data = new ReportData([_flat, _cottage], [_water, _power], [water, power], [], [], bills, [], []);
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static Bill NewBill(Contract contract, decimal gross, YearMonth period) => new()
    {
        PropertyId = contract.PropertyId,
        CategoryId = contract.CategoryId,
        ContractId = contract.Id,
        Period = period,
        Gross = gross,
        Kind = BillKind.Regular,
    };

    private readonly FakeLayoutModeService _layout = new();

    private ReportsViewModel Create() =>
        new(_loader, _excel, _files, _kit.Clock, _kit.Localizer, _kit.Navigation, _kit.Toast, _state, _layout, _kit.Dialogs);

    private async Task<ReportsViewModel> LoadAsync()
    {
        var vm = Create();
        await vm.OnAppearingAsync();
        return vm;
    }

    private static string[] Texts(ReportRowViewModel row) => [.. row.Cells.Select(c => c.Text)];

    [Fact]
    public async Task The_chart_is_built_from_the_table_and_the_phone_shows_either_of_them()
    {
        var vm = await LoadAsync();

        Assert.True(vm.ShowViewSwitch);
        Assert.True(vm.ShowTable);
        Assert.False(vm.ShowChart);
        var chart = Assert.Single(vm.Charts);
        Assert.Equal(90m, chart.Series.Sum(s => s.Points.Sum(p => p.Value ?? 0m)));

        vm.ViewModes[1].ChooseCommand.Execute(null);

        Assert.True(_state.ShowChart);
        Assert.True(vm.ShowChart);
        Assert.False(vm.ShowTable);
        Assert.True(vm.ViewModes[1].IsSelected);
        Assert.False(vm.ViewModes[0].IsSelected);

        vm.ViewModes[0].ChooseCommand.Execute(null);

        Assert.True(vm.ShowTable);
        Assert.False(vm.ShowChart);
    }

    [Fact]
    public async Task The_chart_choice_is_remembered_and_wide_windows_show_chart_and_table_together()
    {
        _state.ShowChart = true;
        var vm = await LoadAsync();
        Assert.True(vm.ShowChart);
        Assert.False(vm.ShowTable);

        _layout.Change(LayoutMode.Expanded);

        Assert.True(vm.ShowChart);
        Assert.True(vm.ShowTable);
        Assert.False(vm.ShowViewSwitch);
    }

    [Fact]
    public async Task Without_data_there_is_no_chart_and_no_switch()
    {
        _loader.Data = new ReportData([_flat], [_water], [], [], [], [], [], []);
        var vm = await LoadAsync();

        Assert.Empty(vm.Charts);
        Assert.False(vm.ShowChart);
        Assert.False(vm.ShowTable);
        Assert.False(vm.ShowViewSwitch);
        Assert.True(vm.ShowEmpty);
    }

    [Fact]
    public async Task The_costs_of_this_year_by_category_are_shown_when_the_screen_appears()
    {
        var vm = await LoadAsync();

        Assert.Equal("SectionReports [en]", vm.Title);
        Assert.Equal(["01.2026", "12.2026"], [vm.FromText, vm.ToText]);
        Assert.Equal(ReportKind.Costs, vm.Kind);
        Assert.True(vm.ShowTable);
        Assert.False(vm.ShowEmpty);

        var table = vm.TableView;
        Assert.Equal(["Flat", "Cottage", ""], table.Groups.Select(g => g.Text));
        Assert.Equal(["Enum_ServiceKind_ColdWater [en]", "Enum_ServiceKind_Electricity [en]"], table.ColumnHeaders);
        Assert.Equal("ReportTotal [en]", table.TotalHeader);
        Assert.Equal(13, table.Rows.Count);
        Assert.Equal("01.2026", table.Rows[0].Label);
        Assert.Equal(["20.00", ""], Texts(table.Rows[0]));
        Assert.Equal("20.00", table.Rows[0].Total!.Text);
        Assert.Equal(["30.00", "40.00"], Texts(table.Rows[1]));
        Assert.Equal("70.00", table.Rows[1].Total!.Text);
        var totals = table.Rows[^1];
        Assert.True(totals.IsTotalsRow);
        Assert.Equal(["ReportTotal [en]", "50.00", "40.00", "90.00"], [totals.Label, .. Texts(totals), totals.Total!.Text]);
        Assert.Equal(1, _loader.Loads);
    }

    [Fact]
    public async Task The_options_of_each_report_differ()
    {
        var vm = await LoadAsync();
        Assert.Equal(
            (true, true, true, true, false, true),
            (vm.ShowGrouping, vm.ShowMeasure, vm.ShowYearOverYear, vm.ShowRowModes, vm.ShowPerMeter, vm.ShowCategories));
        Assert.Equal(["Gross", "Net", "Vat"], vm.Measures.Select(m => ((ReportMeasure)m.Value!).ToString()));

        vm.Kinds[(int)ReportKind.Consumption].ChooseCommand.Execute(null);
        await Settled(vm);

        Assert.Equal((false, false, true, true), (vm.ShowGrouping, vm.ShowMeasure, vm.ShowYearOverYear, vm.ShowPerMeter));
        Assert.True(vm.Kinds[(int)ReportKind.Consumption].IsSelected);
        Assert.False(vm.Kinds[(int)ReportKind.Costs].IsSelected);

        vm.Kinds[(int)ReportKind.CalculatedVsBilled].ChooseCommand.Execute(null);
        await Settled(vm);

        Assert.Equal(5, vm.Measures.Count);
        Assert.True(vm.Measures[0].IsSelected);
        Assert.False(vm.ShowYearOverYear);
    }

    /// <summary>Runs a filter dialog: <paramref name="script"/> changes it, then it is confirmed.</summary>
    private async Task RunDialogAsync<T>(ReportsViewModel vm, Func<Task> open, Action<T> script)
        where T : EditorDialogViewModel
    {
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = Assert.IsType<T>(editor);
            script(dialog);
            (dialog as MultiSelectDialogViewModel)?.ConfirmCommand.Execute(null);
            (dialog as ReportPeriodDialogViewModel)?.ConfirmCommand.Execute(null);
        };
        await open();
        await Settled(vm);
    }

    private Task ChoosePreset(ReportsViewModel vm, ReportPreset preset) =>
        RunDialogAsync<ReportPeriodDialogViewModel>(vm, () => vm.ChoosePeriodCommand.ExecuteAsync(null), d => d.Presets[(int)preset].ChooseCommand.Execute(null));

    [Fact]
    public async Task Presets_set_the_months_and_the_period_field_shows_them()
    {
        var vm = await LoadAsync();
        Assert.Equal("ReportPresetThisYear [en] (01.2026–12.2026)", vm.PeriodFieldText);

        await ChoosePreset(vm, ReportPreset.Last12Months);
        Assert.Equal(["11.2025", "10.2026"], [vm.FromText, vm.ToText]);
        Assert.Equal("ReportPresetLast12Months [en] (11.2025–10.2026)", vm.PeriodFieldText);

        await ChoosePreset(vm, ReportPreset.LastYear);
        Assert.Equal(["01.2025", "12.2025"], [vm.FromText, vm.ToText]);

        await ChoosePreset(vm, ReportPreset.All);
        // The first month with data to the current month (the bills end in February).
        Assert.Equal(["01.2026", "10.2026"], [vm.FromText, vm.ToText]);
        Assert.Equal(11, vm.TableView.Rows.Count);
    }

    [Fact]
    public async Task A_custom_range_is_picked_from_the_month_lists()
    {
        var vm = await LoadAsync();

        await RunDialogAsync<ReportPeriodDialogViewModel>(vm, () => vm.ChoosePeriodCommand.ExecuteAsync(null), d =>
        {
            Assert.False(d.IsCustom);
            d.Presets[(int)ReportPreset.Custom].ChooseCommand.Execute(null);
            Assert.True(d.IsCustom);
            d.FromIndex = d.MonthTexts.ToList().IndexOf("02.2026");
            d.ToIndex = d.MonthTexts.ToList().IndexOf("04.2026");
        });

        Assert.Equal(["02.2026", "04.2026"], [vm.FromText, vm.ToText]);
        Assert.Equal("02.2026–04.2026", vm.PeriodFieldText);
        Assert.Equal(4, vm.TableView.Rows.Count);
    }

    [Fact]
    public async Task A_custom_range_that_ends_before_it_starts_is_refused_in_the_dialog()
    {
        var vm = await LoadAsync();
        var (from, to) = (new YearMonth(2026, 1), new YearMonth(2026, 12));
        var dialog = new ReportPeriodDialogViewModel(_kit.Localizer, ReportPreset.Custom, from, to, from, to)
        {
            FromIndex = 5,
            ToIndex = 2,
        };

        dialog.ConfirmCommand.Execute(null);

        Assert.False(dialog.Saved);
        Assert.Equal("ReportPeriodInvalid [en]", dialog.Error);
        Assert.NotNull(vm);
    }

    [Fact]
    public async Task Cancelling_the_period_dialog_changes_nothing()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.EditorScript = editor => ((ReportPeriodDialogViewModel)editor).Presets[2].ChooseCommand.Execute(null);

        await vm.ChoosePeriodCommand.ExecuteAsync(null);

        Assert.Equal(ReportPreset.ThisYear, _state.Preset);
        Assert.Equal(["01.2026", "12.2026"], [vm.FromText, vm.ToText]);
    }

    [Fact]
    public async Task Properties_are_picked_with_check_boxes_and_the_field_shows_the_choice()
    {
        var vm = await LoadAsync();
        Assert.Equal(["Flat", "Cottage"], vm.PropertyChoices.Select(p => p.Text));
        Assert.Equal("ReportFieldAll 2 [en]", vm.PropertiesFieldText);

        await RunDialogAsync<MultiSelectDialogViewModel>(vm, () => vm.ChoosePropertiesCommand.ExecuteAsync(null), d =>
        {
            Assert.All(d.Items, i => Assert.True(i.IsSelected));
            d.Items[1].IsSelected = false;
        });

        Assert.Equal(["Flat", ""], vm.TableView.Groups.Select(g => g.Text));
        Assert.Equal("Flat", vm.PropertiesFieldText);
        Assert.Equal([true, false], vm.PropertyChoices.Select(p => p.IsSelected));
        Assert.Contains("Flat", vm.SummaryText);
    }

    [Fact]
    public async Task Cancelling_a_multi_select_dialog_leaves_the_choices_untouched()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.EditorScript = editor => ((MultiSelectDialogViewModel)editor).SelectNoneCommand.Execute(null);

        await vm.ChoosePropertiesCommand.ExecuteAsync(null);

        Assert.All(vm.PropertyChoices, p => Assert.True(p.IsSelected));
        Assert.Equal(["Flat", "Cottage", ""], vm.TableView.Groups.Select(g => g.Text));
    }

    [Fact]
    public async Task An_archived_property_is_not_selected_until_the_owner_asks_for_it()
    {
        _cottage.IsArchived = true;

        var vm = await LoadAsync();

        Assert.Equal([true, false], vm.PropertyChoices.Select(p => p.IsSelected));
        Assert.Equal(["Flat", ""], vm.TableView.Groups.Select(g => g.Text));
        Assert.Equal("50.00", vm.TableView.Rows[^1].Total!.Text);
    }

    [Fact]
    public async Task Categories_filter_the_costs()
    {
        var vm = await LoadAsync();
        Assert.True(vm.ShowCategories);

        await RunDialogAsync<MultiSelectDialogViewModel>(vm, () => vm.ChooseCategoriesCommand.ExecuteAsync(null), d => d.Items[1].IsSelected = false);

        Assert.False(vm.CategoryChoices[1].IsSelected);
        Assert.Equal("50.00", vm.TableView.Rows[^1].Total!.Text);
        Assert.Equal("Enum_ServiceKind_ColdWater [en]", vm.CategoriesFieldText);
    }

    [Fact]
    public void Selection_summaries_are_all_the_names_or_a_count()
    {
        MultiSelectItemViewModel[] Items(int count, params int[] selected) =>
            [.. Enumerable.Range(0, count).Select(i => new MultiSelectItemViewModel(Guid.NewGuid(), "Item " + i, string.Empty, selected.Contains(i)))];

        Assert.Equal("ReportFieldAll 3 [en]", ReportsViewModel.SummarizeSelection(_kit.Localizer, Items(3, 0, 1, 2)));
        Assert.Equal("Item 0, Item 2", ReportsViewModel.SummarizeSelection(_kit.Localizer, Items(3, 0, 2)));
        Assert.Equal("ReportFieldSome 4|15 [en]", ReportsViewModel.SummarizeSelection(_kit.Localizer, Items(15, 0, 1, 2, 3)));
    }

    [Fact]
    public async Task The_filter_panel_is_collapsed_by_default_on_phones_and_the_choice_is_remembered()
    {
        _layout.Change(LayoutMode.Compact);
        var phone = await LoadAsync();
        Assert.False(phone.FiltersExpanded);
        Assert.True(phone.ShowFilterSummary);
        Assert.Equal("ReportPresetThisYear [en] · ReportAllProperties [en] · ReportAllCategories [en] · ReportGroupCategory [en] · ReportMeasureGross [en]", phone.SummaryText);

        phone.ToggleFiltersCommand.Execute(null);

        Assert.True(phone.FiltersExpanded);
        Assert.True(_state.FiltersExpanded);

        // Leaving the screen and coming back creates a new view model over the same state.
        var again = Create();
        Assert.True(again.FiltersExpanded);
    }

    [Fact]
    public async Task The_filter_panel_is_open_by_default_on_wide_windows_and_has_more_columns()
    {
        _layout.Change(LayoutMode.Expanded);
        var vm = await LoadAsync();
        Assert.True(vm.FiltersExpanded);
        Assert.Equal(3, vm.FilterColumns);

        _layout.Change(LayoutMode.Medium);
        Assert.Equal(2, vm.FilterColumns);

        _layout.Change(LayoutMode.Compact);
        Assert.Equal(1, vm.FilterColumns);
        Assert.False(vm.FiltersExpanded);
    }

    [Fact]
    public async Task Picker_indexes_choose_the_option()
    {
        var vm = await LoadAsync();
        Assert.Equal((int)CostGrouping.Category, vm.GroupingIndex);

        vm.GroupingIndex = (int)CostGrouping.Property;
        await Settled(vm);
        Assert.Equal(CostGrouping.Property, _state.Grouping);
        Assert.Equal((int)CostGrouping.Property, vm.GroupingIndex);

        vm.GroupingIndex = -1;
        await Settled(vm);
        Assert.Equal(CostGrouping.Property, _state.Grouping);

        vm.MeasureIndex = 1;
        await Settled(vm);
        Assert.Equal(ReportMeasure.Net, _state.CostMeasure);
    }

    // ---- The spinner ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_spinner_is_on_during_the_first_load_and_off_after_it()
    {
        var vm = Create();
        var gate = new TaskCompletionSource();
        _loader.Gate = gate.Task;

        var appearing = vm.OnAppearingAsync();
        Assert.True(vm.IsBusy);

        gate.SetResult();
        await appearing;

        Assert.False(vm.IsBusy);
        Assert.True(vm.ShowTable);
    }

    [Fact]
    public async Task Overlapping_loads_do_not_leave_the_spinner_on()
    {
        var vm = Create();
        var first = new TaskCompletionSource();
        _loader.Gate = first.Task;
        var one = vm.OnAppearingAsync();
        var two = vm.ReloadAsync();
        Assert.True(vm.IsBusy);

        first.SetResult();
        await Task.WhenAll(one, two);

        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task A_failing_load_stops_the_spinner_and_reports_the_error()
    {
        var reporter = new RecordingErrorReporter();
        var previous = ViewModelBase.ErrorReporter;
        ViewModelBase.ErrorReporter = reporter;
        try
        {
            _loader.Failure = new InvalidOperationException("boom");
            var vm = Create();

            await vm.OnAppearingAsync();

            Assert.False(vm.IsBusy);
            Assert.Equal("boom", Assert.Single(reporter.Errors).Message);
        }
        finally
        {
            ViewModelBase.ErrorReporter = previous;
        }
    }

    [Fact]
    public async Task Without_an_error_reporter_a_failing_load_still_stops_the_spinner()
    {
        _loader.Failure = new InvalidOperationException("boom");
        var vm = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(vm.OnAppearingAsync);

        Assert.False(vm.IsBusy);
    }

    private sealed class RecordingErrorReporter : IErrorReporter
    {
        public List<Exception> Errors { get; } = [];

        public Task ReportAsync(string source, Exception exception)
        {
            Errors.Add(exception);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Grouping_rows_and_year_over_year_change_the_table()
    {
        var vm = await LoadAsync();

        vm.GroupingIndex = (int)CostGrouping.Property;
        await Settled(vm);
        Assert.Equal(["ReportMeasureGross [en]", "ReportMeasureGross [en]"], vm.TableView.ColumnHeaders);

        vm.RowModeIndex = 1;
        await Settled(vm);
        Assert.Equal(["2026", "ReportTotal [en]"], vm.TableView.Rows.Select(r => r.Label));

        vm.YearOverYear = true;
        await Settled(vm);
        Assert.False(vm.ShowRowModes);
        Assert.Equal(13, vm.TableView.Rows.Count);
        Assert.Equal("ReportTotal [en]", vm.TableView.Rows[^1].Label);
        Assert.Equal("January", vm.TableView.Rows[0].Label);
        Assert.Null(vm.TableView.TotalHeader);
    }

    [Fact]
    public async Task A_report_without_data_shows_the_empty_text_and_cannot_be_exported()
    {
        var vm = await LoadAsync();

        await ChoosePreset(vm, ReportPreset.LastYear);

        Assert.True(vm.ShowEmpty);
        Assert.False(vm.ShowTable);
        Assert.False(vm.CanExport);
        Assert.False(vm.ExportExcelCommand.CanExecute(null));
        Assert.Equal("ReportEmpty [en]", vm.EmptyText);
    }

    [Fact]
    public async Task Texts_follow_a_language_change()
    {
        var vm = await LoadAsync();

        _kit.Language.SetLanguage("ru");
        await Settled(vm);

        Assert.Equal("SectionReports [ru]", vm.Title);
        Assert.Equal("ReportKindCosts [ru]", vm.Kinds[0].Text);
        Assert.Equal("ReportTotal [ru]", vm.TableView.TotalHeader);
    }

    // ---- Tap on a number ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_cell_opens_the_bills_of_its_property_and_month_with_the_category_highlighted()
    {
        var vm = await LoadAsync();

        vm.TableView.Rows[0].Cells[0].OpenCommand!.Execute(null);

        var (route, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.BillsFor, route);
        Assert.Equal(_flat.Id, parameters![EditorNavigation.PropertyIdParameter]);
        Assert.Equal("2026-01", parameters[BillsViewModel.MonthParameter]);
        Assert.Equal(_water.Id, parameters[BillsViewModel.CategoryParameter]);
    }

    [Fact]
    public async Task The_totals_row_of_a_calendar_year_opens_the_year_and_an_empty_cell_does_nothing()
    {
        var vm = await LoadAsync();

        vm.TableView.Rows[^1].Cells[0].OpenCommand!.Execute(null);

        var (_, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(2026, parameters![BillsViewModel.YearParameter]);
        Assert.False(parameters.ContainsKey(BillsViewModel.MonthParameter));
        Assert.Null(vm.TableView.Rows[0].Cells[1].OpenCommand);
    }

    [Fact]
    public void Bills_parameters_for_a_range_of_several_months_show_every_year_and_a_contract_is_highlighted()
    {
        var contractId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var parameters = ReportsViewModel.BillsParameters(
            new ReportDrill(DrillTarget.Bills, propertyId, new YearMonth(2026, 2), new YearMonth(2026, 9), ContractId: contractId));

        Assert.Equal(
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = propertyId,
                [EditorNavigation.ContractIdParameter] = contractId,
            },
            parameters);
    }

    [Fact]
    public async Task A_consumption_cell_opens_the_readings_of_the_property_at_the_end_of_the_month()
    {
        var meter = new Meter { PropertyId = _flat.Id, InstalledOn = new DateOnly(2020, 1, 1) };
        var register = new MeterRegister { MeterId = meter.Id };
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 1, 31), Value = 100m });
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 2, 28), Value = 105m });
        meter.Registers.Add(register);
        _loader.Data = new ReportData([_flat], [_water], [], [], [meter], [], [], []);
        var vm = await LoadAsync();
        vm.Kinds[(int)ReportKind.Consumption].ChooseCommand.Execute(null);
        await Settled(vm);

        Assert.Equal("5.000", vm.TableView.Rows[1].Cells[0].Text);
        vm.TableView.Rows[1].Cells[0].OpenCommand!.Execute(null);

        var (route, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.ReadingsFor, route);
        Assert.Equal(_flat.Id, parameters![EditorNavigation.PropertyIdParameter]);
        Assert.Equal(new DateOnly(2026, 2, 28), parameters[ReadingsViewModel.DateParameter]);
    }

    // ---- Export ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Export_to_excel_writes_the_table_as_shown_to_a_file_named_after_report_and_period()
    {
        var vm = await LoadAsync();

        await vm.ExportExcelCommand.ExecuteAsync(null);

        var saved = Assert.Single(_files.Saved);
        Assert.Equal(("HomeUtilities-costs-2026-01-2026-12.xlsx", ReportsViewModel.ExcelContentType, "Reports"), (saved.FileName, saved.ContentType, saved.Folder));
        var sheet = Assert.Single(_excel.Written);
        Assert.Equal("ReportKindCosts [en]", sheet.Title);
        Assert.Equal(
            ["ReportSheetReport", "ReportSheetPeriod", "ReportPropertiesLabel", "ReportCategoriesLabel", "ReportGroupingLabel", "ReportMeasureLabel", "ReportRowsLabel", "ReportSheetCreated"],
            sheet.Filters.Select(f => f.Label.Split(' ')[0]));
        Assert.Equal("2026-01 – 2026-12", sheet.Filters[1].Value);
        Assert.Equal("ReportAllProperties [en]", sheet.Filters[2].Value);
        Assert.Equal("2026-10-27", sheet.Filters[^1].Value);
        Assert.Equal(2, sheet.HeaderRows.Count);
        Assert.Equal(["", "Flat", "Cottage", ""], sheet.HeaderRows[0]);
        Assert.Equal(13, sheet.Rows.Count);
        Assert.Equal(SheetCellKind.Month, sheet.Rows[0][0].Kind);
        Assert.Equal(new DateOnly(2026, 1, 1), sheet.Rows[0][0].Month);
        Assert.Equal(20m, sheet.Rows[0][1].Number);
        Assert.Equal(2, sheet.Rows[0][1].Decimals);
        Assert.Equal(SheetCellKind.Empty, sheet.Rows[0][2].Kind);
        Assert.Equal(90m, sheet.Rows[^1][3].Number);
        Assert.True(sheet.Rows[^1][3].IsBold);
        Assert.Equal("C:\\Reports", Assert.Single(_kit.Toast.Messages).Split(' ')[1]);
    }

    [Fact]
    public async Task Export_to_csv_writes_utf8_with_a_byte_order_mark()
    {
        var vm = await LoadAsync();

        await vm.ExportCsvCommand.ExecuteAsync(null);

        var saved = Assert.Single(_files.Saved);
        Assert.Equal(("HomeUtilities-costs-2026-01-2026-12.csv", "text/csv"), (saved.FileName, saved.ContentType));
        Assert.Equal([0xEF, 0xBB, 0xBF], _files.LastBytes[..3]);
        Assert.Contains("2026-01;20.00;;20.00", Encoding.UTF8.GetString(_files.LastBytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_the_save_dialog_shows_no_message_and_a_shared_file_says_so()
    {
        var vm = await LoadAsync();

        _files.Result = new FileSaveResult(false, null);
        await vm.ExportExcelCommand.ExecuteAsync(null);
        Assert.Empty(_kit.Toast.Messages);

        _files.Result = new FileSaveResult(true, null);
        await vm.ExportExcelCommand.ExecuteAsync(null);
        Assert.Equal("ReportSaved [en]", Assert.Single(_kit.Toast.Messages));
    }

    [Fact]
    public async Task The_file_name_names_the_report_and_the_months()
    {
        var filter = new ReportFilter(new YearMonth(2025, 11), new YearMonth(2026, 10));

        Assert.Equal("HomeUtilities-unit-cost-2025-11-2026-10.xlsx", ReportSheetBuilder.FileName(ReportKind.UnitCost, filter, "xlsx"));
        Assert.Equal("HomeUtilities-calculated-vs-billed-2025-11-2026-10.csv", ReportSheetBuilder.FileName(ReportKind.CalculatedVsBilled, filter, "csv"));
        Assert.Equal("HomeUtilities-consumption-2025-11-2026-10.xlsx", ReportSheetBuilder.FileName(ReportKind.Consumption, filter, "xlsx"));
        await Task.CompletedTask;
    }

    [Fact]
    public void Presets_resolve_to_months()
    {
        var earliest = new YearMonth(2025, 3);
        var latest = new YearMonth(2026, 12);

        Assert.Equal((new YearMonth(2026, 1), new YearMonth(2026, 12)), ReportPeriods.Resolve(ReportPreset.ThisYear, Today, earliest, latest));
        Assert.Equal((new YearMonth(2025, 11), new YearMonth(2026, 10)), ReportPeriods.Resolve(ReportPreset.Last12Months, Today, earliest, latest));
        Assert.Equal((new YearMonth(2025, 1), new YearMonth(2025, 12)), ReportPeriods.Resolve(ReportPreset.LastYear, Today, earliest, latest));
        Assert.Equal((earliest, latest), ReportPeriods.Resolve(ReportPreset.All, Today, earliest, latest));
        Assert.Equal((new YearMonth(2026, 10), new YearMonth(2026, 10)), ReportPeriods.Resolve(ReportPreset.All, Today, null, null));
    }

    private static Task Settled(ReportsViewModel vm) => vm.WhenIdleAsync();

    private sealed class FakeReportLoader : IReportDataLoader
    {
        public ReportData Data { get; set; } = null!;

        public int Loads { get; private set; }

        /// <summary>When set, the load completes only after this task.</summary>
        public Task? Gate { get; set; }

        /// <summary>When set, the load throws it.</summary>
        public Exception? Failure { get; set; }

        public async Task<ReportData> LoadAsync(CancellationToken ct = default)
        {
            Loads++;
            if (Gate is not null)
            {
                await Gate;
            }

            return Failure is null ? Data : throw Failure;
        }
    }

    private sealed class FakeWorkbookWriter : IReportWorkbookWriter
    {
        public List<ReportSheet> Written { get; } = [];

        public void Write(ReportSheet sheet, Stream stream)
        {
            Written.Add(sheet);
            stream.WriteByte(1);
        }
    }

    private sealed class FakeFileSaveService : IFileSaveService
    {
        public FileSaveResult Result { get; set; } = new(true, "C:\\Reports");

        public List<(string FileName, string ContentType, string Folder)> Saved { get; } = [];

        public byte[] LastBytes { get; private set; } = [];

        public async Task<FileSaveResult> SaveAsync(string fileName, string contentType, string folder, Func<Stream, Task> write)
        {
            Saved.Add((fileName, contentType, folder));
            var buffer = new MemoryStream();
            await write(buffer);
            LastBytes = buffer.ToArray();
            return Result;
        }
    }
}
