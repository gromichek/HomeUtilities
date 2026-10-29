using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The chart model of the reports: the same numbers as the table, colours, limits, approximate points and units.</summary>
public class ReportChartBuilderTests : IDisposable
{
    private const string Eur = "Currency_Symbol_EUR [en]";

    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly ContractTestKit _kit = new(Today);
    private readonly Property _flat = new() { Name = "Flat", SortOrder = 1 };
    private readonly Property _cottage = new() { Name = "Cottage", SortOrder = 2 };
    private readonly Category _water = new() { SystemKey = ServiceKind.ColdWater, Name = "Water", Color = "#0000FF", SortOrder = 1 };
    private readonly Category _power = new() { SystemKey = ServiceKind.Electricity, Name = "Power", Color = "#FFAA00", SortOrder = 2 };

    public ReportChartBuilderTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private Dictionary<Guid, string> Colors => new() { [_water.Id] = _water.Color, [_power.Id] = _power.Color };

    private IReadOnlyList<ChartModel> Charts(ReportTable table) =>
        ReportChartBuilder.Build(table, new ReportLabels(_kit.Localizer), _kit.Localizer, Colors);

    private Bill NewBill(Property property, Category category, decimal gross, YearMonth period) => new()
    {
        PropertyId = property.Id,
        CategoryId = category.Id,
        Period = period,
        Gross = gross,
        Kind = BillKind.Regular,
    };

    private ReportData Data(params Bill[] bills) => new([_flat, _cottage], [_water, _power], [], [], [], bills, [], []);

    private static ReportFilter Filter(
        YearMonth from,
        YearMonth to,
        CostGrouping grouping = CostGrouping.Category,
        bool yearOverYear = false,
        ReportMeasure measure = ReportMeasure.Gross) =>
        new(from, to, null, null, ReportRows.Months, yearOverYear, grouping, measure, false);

    // ---- Costs ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Costs_by_category_are_stacked_bars_in_the_colours_of_the_categories()
    {
        // Flat: water 20.00 (Jan), 30.00 (Feb); power 15.00 (Feb).
        var data = Data(
            NewBill(_flat, _water, 20m, new YearMonth(2026, 1)),
            NewBill(_flat, _water, 30m, new YearMonth(2026, 2)),
            NewBill(_flat, _power, 15m, new YearMonth(2026, 2)));
        var table = ReportBuilder.Costs(data, Filter(new YearMonth(2026, 1), new YearMonth(2026, 3)));

        var chart = Assert.Single(Charts(table));

        Assert.Equal(ChartKind.StackedBars, chart.Kind);
        Assert.Equal(["Jan 26", "Feb 26", "Mar 26"], chart.Periods);
        Assert.Equal(["Water", "Power"], chart.Series.Select(s => s.Name));
        Assert.Equal(["#0000FF", "#FFAA00"], chart.Series.Select(s => s.Color));
        Assert.Equal([20m, 30m, null], chart.Series[0].Points.Select(p => p.Value));
        Assert.Equal([null, 15m, null], chart.Series[1].Points.Select(p => p.Value));
        Assert.Equal(Eur, chart.Unit);
        Assert.Equal($"ReportKindCosts [en] · ReportMeasureGross [en] ({Eur})", chart.Title);
        Assert.False(chart.HasApproximate);
        Assert.Null(chart.ApproximateNote);
    }

    [Fact]
    public void Tooltip_names_the_series_the_period_and_the_value()
    {
        var data = Data(NewBill(_flat, _water, 1234.5m, new YearMonth(2026, 1)));
        var table = ReportBuilder.Costs(data, Filter(new YearMonth(2026, 1), new YearMonth(2026, 2)));

        var series = Assert.Single(Assert.Single(Charts(table)).Series);

        Assert.Equal($"Water · Jan 26: 1,234.50 {Eur}", series.Points[0].Tooltip);
        Assert.Equal("Water · Feb 26", series.Points[1].Tooltip);
    }

    [Fact]
    public void Every_point_of_a_chart_is_a_cell_of_the_table()
    {
        var data = Data(
            NewBill(_flat, _water, 20m, new YearMonth(2026, 1)),
            NewBill(_flat, _power, 15m, new YearMonth(2026, 2)),
            NewBill(_cottage, _power, 40m, new YearMonth(2026, 2)),
            NewBill(_cottage, _water, 5.5m, new YearMonth(2026, 3)));

        foreach (var grouping in Enum.GetValues<CostGrouping>())
        {
            var table = ReportBuilder.Costs(data, Filter(new YearMonth(2026, 1), new YearMonth(2026, 3), grouping));
            var chart = Assert.Single(Charts(table));
            var cells = table.Rows.SelectMany(r => r.Cells.Where((_, i) => !table.Columns[i].IsSubtotal)).Sum(c => c.Value ?? 0m);
            var points = chart.Series.SelectMany(s => s.Points).Sum(p => p.Value ?? 0m);

            Assert.Equal(cells, points);
            Assert.Equal(table.Rows.Count, chart.Periods.Count);
            Assert.Equal(80.5m, points);
        }
    }

    [Fact]
    public void The_same_category_of_two_properties_is_one_series_with_the_sum()
    {
        var data = Data(
            NewBill(_flat, _water, 20m, new YearMonth(2026, 2)),
            NewBill(_cottage, _water, 5m, new YearMonth(2026, 2)),
            NewBill(_cottage, _power, 40m, new YearMonth(2026, 2)));
        var table = ReportBuilder.Costs(data, Filter(new YearMonth(2026, 2), new YearMonth(2026, 2)));

        var chart = Assert.Single(Charts(table));

        Assert.Equal(["Water", "Power"], chart.Series.Select(s => s.Name));
        Assert.Equal(25m, chart.Series[0].Points[0].Value);
        Assert.Equal(40m, chart.Series[1].Points[0].Value);
    }

    [Fact]
    public void Costs_by_property_are_bars_side_by_side_named_after_the_properties()
    {
        var data = Data(
            NewBill(_flat, _water, 20m, new YearMonth(2026, 2)),
            NewBill(_cottage, _power, 40m, new YearMonth(2026, 2)));
        var table = ReportBuilder.Costs(data, Filter(new YearMonth(2026, 2), new YearMonth(2026, 2), CostGrouping.Property));

        var chart = Assert.Single(Charts(table));

        Assert.Equal(ChartKind.Bars, chart.Kind);
        Assert.Equal(["Flat", "Cottage"], chart.Series.Select(s => s.Name));
        Assert.Equal([ReportChartBuilder.Palette[0], ReportChartBuilder.Palette[1]], chart.Series.Select(s => s.Color));
    }

    [Fact]
    public void Subtotal_columns_are_not_drawn()
    {
        var data = Data(
            NewBill(_flat, _water, 20m, new YearMonth(2026, 2)),
            NewBill(_flat, _power, 10m, new YearMonth(2026, 2)),
            NewBill(_cottage, _power, 40m, new YearMonth(2026, 2)));
        var table = ReportBuilder.Costs(data, Filter(new YearMonth(2026, 2), new YearMonth(2026, 2)));
        Assert.Contains(table.Columns, c => c.IsSubtotal);

        var chart = Assert.Single(Charts(table));

        Assert.Equal(70m, chart.Series.Sum(s => s.Points[0].Value));
    }

    [Fact]
    public void Costs_year_over_year_have_one_series_per_year_over_the_twelve_months()
    {
        var data = Data(
            NewBill(_flat, _water, 20m, new YearMonth(2025, 2)),
            NewBill(_flat, _power, 5m, new YearMonth(2025, 2)),
            NewBill(_flat, _water, 30m, new YearMonth(2026, 2)));
        var table = ReportBuilder.Costs(data, Filter(new YearMonth(2025, 1), new YearMonth(2026, 12), yearOverYear: true));

        var chart = Assert.Single(Charts(table));

        Assert.Equal(ChartKind.Bars, chart.Kind);
        Assert.Equal(12, chart.Periods.Count);
        Assert.Equal("Jan", chart.Periods[0]);
        Assert.Equal(["2025", "2026"], chart.Series.Select(s => s.Name));
        Assert.Equal(25m, chart.Series[0].Points[1].Value);
        Assert.Equal(30m, chart.Series[1].Points[1].Value);
        Assert.Null(chart.Series[1].Points[0].Value);
    }

    [Fact]
    public void More_than_eight_series_keep_the_seven_largest_and_sum_the_rest_as_other()
    {
        var columns = Enumerable.Range(1, 10).Select(i => Column($"c{i}", $"Contract {i}", ColumnSubject.Contract)).ToList();
        var cells = columns.Select((_, i) => new ReportCell(i + 1)).ToList();
        var table = Table(ReportKind.Costs, columns, [Row(1, cells)]);

        var chart = Assert.Single(Charts(table));

        Assert.Equal(ReportChartBuilder.MaxSeries, chart.Series.Count);
        var other = chart.Series[^1];
        Assert.Equal("ReportChartOther [en]", other.Name);
        Assert.Equal(ReportChartBuilder.OtherColor, other.Color);
        // Contracts 1, 2 and 3 (1 + 2 + 3 = 6) are the smallest three.
        Assert.Equal(6m, other.Points[0].Value);
        Assert.Equal(55m, chart.Series.Sum(s => s.Points[0].Value));
    }

    [Fact]
    public void Nothing_to_draw_gives_no_chart()
    {
        var empty = ReportBuilder.Costs(Data(), Filter(new YearMonth(2026, 1), new YearMonth(2026, 12)));
        Assert.True(empty.IsEmpty);
        Assert.Empty(Charts(empty));

        var onlyGaps = Table(ReportKind.Costs, [Column("c", "Water", ColumnSubject.Contract)], [Row(1, [ReportCell.Empty])]);
        Assert.Empty(Charts(onlyGaps));
    }

    // ---- Approximate points -----------------------------------------------------------------------------------------

    [Fact]
    public void Approximate_points_are_marked_and_the_chart_explains_them()
    {
        var table = Table(
            ReportKind.Consumption,
            [Column("w", "Water", ColumnSubject.Resource, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.M3, MeterResource.ColdWater)],
            [Row(1, [new ReportCell(5m)]), Row(2, [new ReportCell(6m, IsApproximate: true)]), Row(3, [new ReportCell(null, IsApproximate: true)])]);

        var chart = Assert.Single(Charts(table));

        Assert.Equal([false, true, true], chart.Series[0].Points.Select(p => p.IsApproximate));
        Assert.Equal([5m, 6m, null], chart.Series[0].Points.Select(p => p.Value));
        Assert.True(chart.HasApproximate);
        Assert.Equal("ReportChartApproximate [en]", chart.ApproximateNote);
        Assert.Equal("Enum_MeterResource_ColdWater [en], Enum_MeterUnit_M3 [en] · Feb 26: ≈ 6.000 Enum_MeterUnit_M3 [en]", chart.Series[0].Points[1].Tooltip);
    }

    // ---- Consumption, unit cost, comparison -------------------------------------------------------------------------

    [Fact]
    public void Consumption_is_one_line_chart_per_unit()
    {
        var table = Table(
            ReportKind.Consumption,
            [
                Column("cold", "Cold", ColumnSubject.Resource, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.M3, MeterResource.ColdWater),
                Column("hot", "Hot", ColumnSubject.Resource, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.M3, MeterResource.HotWater),
                Column("el", "El", ColumnSubject.Resource, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.KWh, MeterResource.Electricity),
            ],
            [Row(1, [new ReportCell(10m), new ReportCell(4m), new ReportCell(300m)]), Row(2, [new ReportCell(12m), new ReportCell(5m), new ReportCell(250m)])]);

        var charts = Charts(table);

        Assert.Equal(2, charts.Count);
        Assert.All(charts, c => Assert.Equal(ChartKind.Lines, c.Kind));
        Assert.Equal(2, charts[0].Series.Count);
        Assert.Equal("Enum_MeterUnit_M3 [en]", charts[0].Unit);
        Assert.Equal([10m, 12m], charts[0].Series[0].Points.Select(p => p.Value));
        Assert.Single(charts[1].Series);
        Assert.Equal([300m, 250m], charts[1].Series[0].Points.Select(p => p.Value));
        Assert.StartsWith("ReportKindConsumption [en], ", charts[1].Title);
    }

    [Fact]
    public void Consumption_by_contract_is_a_line_per_contract_with_the_category_colour_used_once()
    {
        // Water and Heating are both in the "Water" category (blue): the first keeps the colour, the second gets a palette colour.
        var water = Column("water", "Water", ColumnSubject.Contract, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.M3) with { CategoryId = _water.Id };
        var heating = Column("heating", "Heating", ColumnSubject.Contract, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.M3) with { CategoryId = _water.Id };
        var power = Column("power", "Power", ColumnSubject.Contract, ReportValueKind.Quantity, ReportMeasure.Quantity, MeterUnit.KWh) with { CategoryId = _power.Id };
        var table = Table(
            ReportKind.Consumption,
            [water, heating, power],
            [Row(1, [new ReportCell(14m), new ReportCell(5m), new ReportCell(200m)])]);

        var charts = Charts(table);

        Assert.Equal(2, charts.Count);
        Assert.Equal(["Water", "Heating"], charts[0].Series.Select(x => x.Name));
        Assert.Equal(_water.Color, charts[0].Series[0].Color);
        Assert.NotEqual(_water.Color, charts[0].Series[1].Color);
        Assert.Equal(_power.Color, charts[1].Series[0].Color);
    }

    [Fact]
    public void Unit_cost_is_a_line_per_contract_and_the_extra_lines_are_not_summed()
    {
        var columns = Enumerable.Range(1, 10)
            .Select(i => Column($"c{i}", $"Contract {i}", ColumnSubject.Contract, ReportValueKind.UnitPrice, ReportMeasure.Gross, MeterUnit.M3))
            .ToList();
        var table = Table(ReportKind.UnitCost, columns, [Row(1, columns.Select((_, i) => new ReportCell(1.5m + i)).ToList())]);

        var chart = Assert.Single(Charts(table));

        Assert.Equal(ChartKind.Lines, chart.Kind);
        Assert.Equal(ReportChartBuilder.MaxSeries, chart.Series.Count);
        Assert.DoesNotContain(chart.Series, s => s.Name == "ReportChartOther [en]");
        Assert.Contains($"{Eur}/", chart.Unit);
    }

    [Fact]
    public void Calculated_vs_billed_difference_is_bars_around_zero_and_the_loss_is_a_line()
    {
        var difference = Table(
            ReportKind.CalculatedVsBilled,
            [Column("w", "Water", ColumnSubject.Contract, ReportValueKind.Money, ReportMeasure.Difference)],
            [Row(1, [new ReportCell(2.5m)]), Row(2, [new ReportCell(-1.25m)])]);
        var loss = Table(
            ReportKind.CalculatedVsBilled,
            [Column("w", "Water", ColumnSubject.Contract, ReportValueKind.Quantity, ReportMeasure.QuantityDifference, MeterUnit.M3)],
            [Row(1, [new ReportCell(0.4m)]), Row(2, [new ReportCell(-0.1m)])]);

        var bars = Assert.Single(Charts(difference));
        var line = Assert.Single(Charts(loss));

        Assert.Equal(ChartKind.Bars, bars.Kind);
        Assert.Equal([2.5m, -1.25m], bars.Series[0].Points.Select(p => p.Value));
        Assert.Equal(ChartKind.Lines, line.Kind);
        Assert.Equal([0.4m, -0.1m], line.Series[0].Points.Select(p => p.Value));
    }

    // ---- Axis ---------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 87, true, 0, 100, 25)]
    [InlineData(0, 7.3, true, 0, 8, 2)]
    [InlineData(-1.25, 2.5, true, -2, 3, 1)]
    [InlineData(102, 118, false, 100, 120, 5)]
    [InlineData(0, 0, true, 0, 1, 0.25)]
    [InlineData(5, 5, false, 4.5, 5.5, 0.25)]
    public void Axis_scales_use_round_steps_that_cover_the_data(double low, double high, bool zero, double min, double max, double step)
    {
        var scale = ChartScale.For((decimal)low, (decimal)high, zero);

        Assert.Equal((decimal)min, scale.Min);
        Assert.Equal((decimal)max, scale.Max);
        Assert.Equal((decimal)step, scale.Step);
        Assert.Equal(scale.Min, scale.Ticks().First());
        Assert.Equal(scale.Max, scale.Ticks().Last());
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------

    private ReportColumn Column(
        string key,
        string name,
        ColumnSubject subject,
        ReportValueKind kind = ReportValueKind.Money,
        ReportMeasure measure = ReportMeasure.Gross,
        MeterUnit? unit = null,
        MeterResource? resource = null) =>
        new(key, _flat.Id, _flat.Name, subject, name, null, resource, unit, null, measure, kind);

    private static ReportRow Row(int month, IReadOnlyList<ReportCell> cells) =>
        new(ReportRowKind.Month, 2026, month, cells, null);

    private ReportTable Table(ReportKind kind, IReadOnlyList<ReportColumn> columns, IReadOnlyList<ReportRow> rows) =>
        new(kind, Filter(new YearMonth(2026, 1), new YearMonth(2026, rows.Count)), columns, rows, null, false);
}
