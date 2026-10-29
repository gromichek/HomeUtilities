using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using HomeUtilities.Data.Sample;
using HomeUtilities.Data.Stores;
using HomeUtilities.ViewModels;

namespace HomeUtilities.Data.Tests.Sample;

/// <summary>The sample data (13 months) gives every report plausible numbers, and the costs agree with the bills.</summary>
public class ReportSampleDataTests
{
    private static async Task<(ReportData Data, YearMonth Today)> LoadAsync(DateTimeOffset now)
    {
        using var test = new TestDb();
        test.Clock.UtcNow = now;
        using (var db = test.NewContext())
        {
            await DataSeeder.SeedAsync(db, CancellationToken.None);
        }

        var factory = test.Factory;
        var generator = new SampleDataGenerator(
            new PropertyStore(factory), new SupplierStore(factory), new CategoryStore(factory), new ContractStore(factory),
            new MeterStore(factory), new TariffStore(factory), new EqualizedPlanStore(factory), new BillTemplateStore(factory, test.Clock), new BillStore(factory),
            new PaymentStore(factory), new ImportProfileStore(factory), new ChargeInputLoader(factory), test.Clock);
        Assert.True(await generator.FillAsync());

        return (await new ReportDataLoader(factory).LoadAsync(), YearMonth.From(test.Clock.Today));
    }

    private static ReportFilter Filter(YearMonth from, YearMonth to, Action<ReportFilterOptions>? configure = null)
    {
        var options = new ReportFilterOptions();
        configure?.Invoke(options);
        return new ReportFilter(from, to, null, null, options.Rows, options.YearOverYear, options.Grouping, options.Measure, options.PerMeter);
    }

    private sealed class ReportFilterOptions
    {
        public ReportRows Rows { get; set; }

        public bool YearOverYear { get; set; }

        public CostGrouping Grouping { get; set; }

        public ReportMeasure Measure { get; set; }

        public bool PerMeter { get; set; }
    }

    [Fact]
    public async Task Reports_contain_no_draft_bills()
    {
        var (data, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

        Assert.NotEmpty(data.Bills);
        Assert.All(data.Bills, b => Assert.False(b.IsDraft));

        // the prepaid internet: the drafts of the previous and the current month are not costs yet
        var internet = data.Contracts.Single(c => c.Service == ServiceKind.InternetTv);
        Assert.DoesNotContain(data.Bills, b => b.ContractId == internet.Id && (b.Period == today || b.Period == today.AddMonths(-1)));
    }

    [Fact]
    public async Task Costs_total_the_same_as_the_bills_of_the_period()
    {
        var (data, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
        var from = new YearMonth(2026, 1);
        var to = new YearMonth(2026, 12);

        foreach (var grouping in Enum.GetValues<CostGrouping>())
        {
            var table = ReportBuilder.Costs(data, Filter(from, to, o => o.Grouping = grouping));
            var expected = data.Bills.Where(b => b.Period >= from && b.Period <= to).Sum(b => b.Gross);

            Assert.False(table.IsEmpty);
            Assert.Equal(expected, table.Totals!.Total!.Value);
            Assert.Equal(expected, table.Rows.Sum(r => r.Total!.Value));
        }

        Assert.True(today >= from);
    }

    [Fact]
    public async Task Each_property_costs_what_its_bills_say()
    {
        var (data, _) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
        var from = new YearMonth(2025, 1);
        var to = new YearMonth(2026, 12);

        var table = ReportBuilder.Costs(data, Filter(from, to, o => o.Grouping = CostGrouping.Property));

        Assert.True(table.Columns.Count >= 2);
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var propertyId = table.Columns[i].PropertyId;
            Assert.Equal(data.Bills.Where(b => b.PropertyId == propertyId).Sum(b => b.Gross), table.Totals!.Cells[i].Value);
        }
    }

    [Fact]
    public async Task Consumption_unit_cost_and_comparison_show_plausible_numbers()
    {
        var (data, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
        var to = today.AddMonths(-1);
        var from = to.AddMonths(-11);
        var filter = Filter(from, to);

        var consumption = ReportBuilder.Consumption(data, filter);
        var water = consumption.Columns.Select((c, i) => (c, i)).First(x => x.c is { Resource: MeterResource.ColdWater, Register: RegisterKind.Total });
        var cold = consumption.Rows.Select(r => r.Cells[water.i]).ToList();
        Assert.Equal(12, cold.Count);
        Assert.True(cold.Count(c => c.Value is > 0m and < 30m) >= 10, "Cold water: a few m3 per month.");

        var unit = ReportBuilder.UnitCost(data, filter);
        Assert.NotEmpty(unit.Columns);
        Assert.Contains(unit.Rows.SelectMany(r => r.Cells), c => c.Value is > 0m);
        Assert.All(unit.Rows.SelectMany(r => r.Cells).Where(c => c.Value is not null), c => Assert.InRange(c.Value!.Value, 0.01m, 50m));

        var compared = ReportBuilder.CalculatedVsBilled(data, filter);
        Assert.NotEmpty(compared.Columns);
        Assert.Contains(compared.Rows.SelectMany(r => r.Cells), c => c.Value is not null);

        var losses = ReportBuilder.CalculatedVsBilled(data, Filter(from, to, o => o.Measure = ReportMeasure.QuantityDifference));
        Assert.Contains(losses.Rows.SelectMany(r => r.Cells), c => c.Value is > 0m);
    }

    [Fact]
    public async Task Every_report_runs_in_every_mode_over_the_sample_data()
    {
        var (data, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
        var all = data.EarliestMonth!.Value;

        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            foreach (var rows in Enum.GetValues<ReportRows>())
            {
                foreach (var yoy in new[] { false, true })
                {
                    foreach (var measure in Enum.GetValues<ReportMeasure>())
                    {
                        var table = ReportBuilder.Build(kind, data, Filter(all, today, o =>
                        {
                            o.Rows = rows;
                            o.YearOverYear = yoy;
                            o.Measure = measure;
                            o.PerMeter = measure == ReportMeasure.Net;
                        }));

                        Assert.All(table.Rows, r => Assert.Equal(table.Columns.Count, r.Cells.Count));
                        Assert.Equal(yoy ? 12 : rows == ReportRows.Years ? today.Year - all.Year + 1 : all.MonthsUntil(today) + 1, table.Rows.Count);
                    }
                }
            }
        }
    }

    private sealed class KeyLocalizer : ILocalizer
    {
        public string this[string key] => key;

        public string Get(string key, params object[] args) => key;

        public bool TryGet(string key, out string value)
        {
            value = key;
            return true;
        }
    }

    [Fact]
    public async Task Every_report_has_a_chart_with_the_numbers_of_its_table_over_the_sample_data()
    {
        var (data, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));
        var all = data.EarliestMonth!.Value;
        var localizer = new KeyLocalizer();
        var labels = new ReportLabels(localizer, System.Globalization.CultureInfo.InvariantCulture);
        var colors = data.Categories.ToDictionary(c => c.Id, c => c.Color);
        var chartsSeen = new HashSet<ReportKind>();

        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            ReportMeasure[] measures = kind switch
            {
                ReportKind.CalculatedVsBilled => [ReportMeasure.Difference, ReportMeasure.Calculated, ReportMeasure.Billed, ReportMeasure.QuantityDifference],
                ReportKind.Consumption => [ReportMeasure.Quantity],
                _ => [ReportMeasure.Gross, ReportMeasure.Net],
            };
            foreach (var measure in measures)
            {
                foreach (var yoy in new[] { false, true })
                {
                    if (yoy && kind is ReportKind.UnitCost or ReportKind.CalculatedVsBilled)
                    {
                        continue;
                    }

                    var table = ReportBuilder.Build(kind, data, Filter(all, today, o =>
                    {
                        o.YearOverYear = yoy;
                        o.Measure = measure;
                    }));
                    var charts = ReportChartBuilder.Build(table, labels, localizer, colors);

                    foreach (var chart in charts)
                    {
                        chartsSeen.Add(kind);
                        Assert.InRange(chart.Series.Count, 1, ReportChartBuilder.MaxSeries);
                        Assert.All(chart.Series, s => Assert.Equal(chart.Periods.Count, s.Points.Count));
                        Assert.All(chart.Series, s => Assert.Matches("^#[0-9A-Fa-f]{6}$", s.Color));
                    }

                    // The chart shows what the table shows: the numbers of the columns that are not subtotals add up the same.
                    // (Year over year charts add up the total registers of a year only, so they are compared for costs.)
                    if (kind is ReportKind.Costs or ReportKind.CalculatedVsBilled && !yoy && measure != ReportMeasure.QuantityDifference)
                    {
                        var inTable = table.Rows.SelectMany(r => r.Cells.Where((_, i) => !table.Columns[i].IsSubtotal)).Sum(c => c.Value ?? 0m);
                        var inCharts = charts.SelectMany(c => c.Series).SelectMany(s => s.Points).Sum(p => p.Value ?? 0m);
                        Assert.Equal(inTable, inCharts);
                    }
                }
            }
        }

        Assert.Equal(Enum.GetValues<ReportKind>().Length, chartsSeen.Count);
    }

    [Fact]
    public async Task Loading_does_not_return_deleted_rows_and_knows_the_earliest_month()
    {
        var (data, today) = await LoadAsync(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero));

        Assert.All(data.Bills, b => Assert.False(b.IsDeleted));
        Assert.True(data.EarliestMonth <= today.AddMonths(-SampleDataGenerator.MonthsOfReadings + 1));
    }
}
