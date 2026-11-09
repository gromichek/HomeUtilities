using System.Globalization;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reports;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The three charts of the Overview (T-038): this year by month, spending by category, consumption. Today is 27 Oct 2026.</summary>
public class OverviewChartsViewModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);
    private readonly ReportFilterState _reports = new();

    public OverviewChartsViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private async Task<OverviewViewModel> LoadAsync()
    {
        var vm = new OverviewViewModel(
            _kit.Kit.Localizer, _kit.Kit.Clock, new Initializer(), new Loader(_kit.Kit), _kit.Templates, _kit.QuickPay, _kit.Kit.Toast,
            _kit.Kit.Navigation, _kit.Kit.Dialogs, _kit.Kit.Settings, _kit.Kit.Layout, reportState: _reports);
        await vm.OnAppearingAsync();
        return vm;
    }

    private static string Money(string amount) => $"{amount} Currency_Symbol_EUR [en]";

    // ---- A: this year by month -----------------------------------------------------------------------------------

    [Fact]
    public async Task Year_chart_has_twelve_months_and_two_series_with_a_text_summary()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 3), 100m);
        _kit.AddBill(water, new YearMonth(2026, 10), 95m);
        _kit.AddBill(water, new YearMonth(2025, 10), 90m);

        var vm = await LoadAsync();

        var chart = vm.YearChart!;
        Assert.Equal(ChartKind.Bars, chart.Kind);
        Assert.Equal(["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"], chart.Periods);
        Assert.Equal(["2026", "2025"], chart.Series.Select(s => s.Name));
        Assert.Equal([OverviewViewModel.AccentColor, OverviewViewModel.MutedColor], chart.Series.Select(s => s.Color));
        Assert.Equal(95m, chart.Series[0].Points[9].Value);
        Assert.Equal(90m, chart.Series[1].Points[9].Value);
        Assert.Null(chart.Series[0].Points[10].Value);   // November: still to come
        Assert.True(vm.HasYearChart);

        // "October 2026: 95,00 €, 2025: 90,00 €" for screen readers (March has no last-year bill).
        Assert.Contains($"OverviewChartMonthWithLast October 2026|{Money("95.00")}|2025|{Money("90.00")} [en]", vm.YearChartSummary);
        Assert.Contains($"OverviewChartMonth March 2026|{Money("100.00")} [en]", vm.YearChartSummary);
    }

    [Fact]
    public async Task Year_chart_without_last_year_has_one_series_and_without_bills_no_chart()
    {
        var empty = await LoadAsync();
        Assert.False(empty.HasYearChart);
        Assert.Null(empty.YearChart);

        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 3), 100m);
        var vm = await LoadAsync();

        Assert.Equal(["2026"], vm.YearChart!.Series.Select(s => s.Name));
    }

    [Fact]
    public async Task A_tap_on_a_month_opens_the_bills_of_that_month_for_all_properties_or_the_filtered_one()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2026, 10), 95m);
        var vm = await LoadAsync();

        await vm.OpenMonthCommand.ExecuteAsync(10);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillsFor, route);
        Assert.Equal("2026-10", parameters![BillsViewModel.MonthParameter]);
        Assert.False(parameters.ContainsKey(EditorNavigation.PropertyIdParameter));

        // With a second property and the filter on the first one, the property travels along.
        var cottage = new Property { Name = "Cottage" };
        _kit.Kit.Properties.Stored.Add(cottage);
        _kit.Kit.Settings.SetString(OverviewViewModel.PropertyFilterKey, _kit.Kit.Property.Id.ToString());
        var filtered = await LoadAsync();
        _kit.Kit.Navigation.Visited.Clear();

        await filtered.OpenMonthCommand.ExecuteAsync(10);

        Assert.Equal(_kit.Kit.Property.Id, Assert.Single(_kit.Kit.Navigation.Visited).Parameters![EditorNavigation.PropertyIdParameter]);
    }

    // ---- B: spending by category ---------------------------------------------------------------------------------

    [Fact]
    public async Task Category_card_lists_the_categories_by_amount_with_shares_and_opens_the_bills()
    {
        var water = _kit.AddWaterContract(tariff: false);
        var power = _kit.AddWaterContract("Power", tariff: false);
        power.CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Electricity);
        _kit.AddBill(water, new YearMonth(2026, 1), 250m);
        _kit.AddBill(power, new YearMonth(2026, 2), 750m);
        _kit.AddBill(water, new YearMonth(2026, 10), 2m);   // 252 water + 750 power = 1002

        var vm = await LoadAsync();

        Assert.Equal("OverviewCategoriesHeader 2026 [en]", vm.CategoriesHeader);
        Assert.Equal($"OverviewCategoriesSubtitle january|october|{Money("1,002.00")} [en]", vm.CategoriesSubtitle);
        Assert.Equal(2, vm.CategoryRows.Count);
        Assert.Equal([Money("750.00"), Money("252.00")], vm.CategoryRows.Select(r => r.AmountText));
        Assert.Equal(["75 %", "25 %"], vm.CategoryRows.Select(r => r.ShareText));
        Assert.Equal([0.75, 0.25], vm.CategorySegments.Select(s => Math.Round(s.Fraction, 2)));
        Assert.True(vm.HasCategories);
        Assert.False(vm.ShowCategoriesEmpty);

        vm.CategoryRows[0].OpenCommand.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Kit.Navigation.Visited);
        Assert.Equal(Routes.BillsFor, route);
        Assert.Equal(_kit.Kit.CategoryIdFor(ServiceKind.Electricity), parameters![BillsViewModel.CategoryParameter]);
        Assert.Equal(2026, parameters[BillsViewModel.YearParameter]);
    }

    [Fact]
    public async Task Category_card_without_bills_this_year_shows_one_line_and_no_chart()
    {
        var water = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(water, new YearMonth(2025, 3), 100m);

        var vm = await LoadAsync();

        Assert.True(vm.ShowCategoriesEmpty);
        Assert.False(vm.HasCategories);
        Assert.Empty(vm.CategorySegments);
        Assert.Equal("OverviewCategoriesEmpty [en]", vm.CategoriesEmptyText);
    }

    // ---- C: consumption ------------------------------------------------------------------------------------------

    /// <summary>A cold-water meter with a reading on the 28th of each month from September 2025 to September 2026.</summary>
    private void AddMeter(params decimal[] monthly)
    {
        var meter = new Meter { PropertyId = _kit.Kit.Property.Id, InstalledOn = new DateOnly(2020, 1, 1), Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, Location = "Bath" };
        var register = new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Total };
        var total = 500m;
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2025, 9, 28), Value = total });
        for (var n = 0; n < monthly.Length; n++)
        {
            total += monthly[n];
            var month = new YearMonth(2025, 10).AddMonths(n);
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(month.Year, month.Month, 28), Value = total });
        }

        meter.Registers.Add(register);
        _kit.Kit.Meters.Stored.Add(meter);
    }

    [Fact]
    public async Task Consumption_row_shows_the_last_complete_month_the_change_and_a_line()
    {
        // October 2025 – August 2026: 10 m3 each, September 2026: 14 m3 (+40 %, above the 30 % threshold).
        AddMeter(10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 14m);

        var vm = await LoadAsync();

        var row = Assert.Single(vm.ConsumptionRows);
        Assert.Equal("14 Enum_MeterUnit_M3 [en]", row.ValueText);
        Assert.Equal("+40 %", row.ChangeText);
        Assert.True(row.IsRise);
        Assert.True(row.HasLine);
        Assert.Equal(12, row.Line!.Values.Count);
        Assert.True(vm.HasConsumption);
        Assert.Contains("OverviewConsumptionRowSummary", row.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consumption_below_the_threshold_is_not_marked_and_without_meters_the_card_is_hidden()
    {
        Assert.False((await LoadAsync()).HasConsumption);

        AddMeter(10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 12m);
        var vm = await LoadAsync();

        var row = Assert.Single(vm.ConsumptionRows);
        Assert.Equal("+20 %", row.ChangeText);
        Assert.False(row.IsRise);
    }

    [Fact]
    public async Task A_tap_on_a_consumption_row_opens_the_consumption_report_for_the_last_twelve_months()
    {
        AddMeter(10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 14m);
        var vm = await LoadAsync();

        vm.ConsumptionRows[0].OpenCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(Routes.Reports, Assert.Single(_kit.Kit.Navigation.Visited).Route);
        Assert.Equal(ReportKind.Consumption, _reports.Kind);
        Assert.Equal(ReportPreset.Custom, _reports.Preset);
        Assert.Equal((new YearMonth(2025, 10), new YearMonth(2026, 9)), (_reports.From!.Value, _reports.To!.Value));
        Assert.Null(_reports.Properties);
        Assert.True(_reports.ChangedExternally);
    }

    // ---- Texts ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_chart_texts_exist_in_both_languages()
    {
        string[] keys =
        [
            "OverviewChartHint", "OverviewChartMonth", "OverviewChartMonthWithLast", "OverviewCategoriesHeader", "OverviewCategoriesSubtitle",
            "OverviewCategoriesSubtitleOne", "OverviewCategoriesEmpty", "OverviewCategoryOther", "OverviewConsumptionHeader",
            "OverviewConsumptionSubtitle", "OverviewConsumptionRise", "OverviewConsumptionRowSummary",
        ];
        foreach (var key in keys)
        {
            Assert.NotEqual($"!{key}!", ResxLocalizer.English[key]);
            Assert.NotEqual($"!{key}!", ResxLocalizer.Russian[key]);
            Assert.NotEmpty(ResxLocalizer.Russian[key]);
        }

        Assert.Equal("Расходы за 2026 по категориям", ResxLocalizer.Russian.Get("OverviewCategoriesHeader", 2026));
        Assert.Equal("январь – октябрь, всего 1 002,00 €", ResxLocalizer.Russian.Get("OverviewCategoriesSubtitle", "январь", "октябрь", "1 002,00 €"));
        Assert.Equal("Счетов за этот год пока нет.", ResxLocalizer.Russian["OverviewCategoriesEmpty"]);
        Assert.Equal("Потребление", ResxLocalizer.Russian["OverviewConsumptionHeader"]);
        Assert.Equal("Прочее", ResxLocalizer.Russian["OverviewCategoryOther"]);
    }

    private sealed class Initializer : IDatabaseInitializer
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Loader(ContractTestKit kit) : IOverviewDataLoader
    {
        public Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default) => Task.FromResult(new OverviewData(
            [.. kit.Properties.Stored.Where(p => !p.IsArchived)],
            [.. kit.Contracts.Stored],
            [.. kit.Contracts.Links],
            [.. kit.Meters.Stored],
            [.. kit.Bills.Stored.Where(b => !b.IsDraft).Select(Copies.Of)],
            [.. kit.Payments.Stored.Select(Copies.Of)],
            new Dictionary<Guid, decimal>(),
            [.. kit.Bills.Stored.Where(b => b.IsDraft).Select(Copies.Of)]) { Categories = [.. kit.Categories.Stored] });
    }
}
