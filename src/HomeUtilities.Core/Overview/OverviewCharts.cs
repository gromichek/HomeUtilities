using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Overview;

/// <summary>The bills of the months of one year and of the year before (chart "This year" of the Overview).</summary>
/// <param name="Year">This year.</param>
/// <param name="Currency">The currency of the amounts (the one most bills are in).</param>
/// <param name="ThisYear">Twelve values, January first; null = nothing (a future month, or no bill).</param>
/// <param name="ThisYearApproximate">Twelve flags: the month may still change (the current month, or a draft bill waits in it).</param>
/// <param name="LastYear">Twelve values of the year before; all null when <paramref name="HasLastYear"/> is false.</param>
/// <param name="LastYearApproximate">Twelve flags, like <paramref name="ThisYearApproximate"/>.</param>
/// <param name="HasLastYear">Whether the year before has any bill; without one there is no second series.</param>
public sealed record MonthlyBars(
    int Year,
    Currency Currency,
    IReadOnlyList<decimal?> ThisYear,
    IReadOnlyList<bool> ThisYearApproximate,
    IReadOnlyList<decimal?> LastYear,
    IReadOnlyList<bool> LastYearApproximate,
    bool HasLastYear);

/// <summary>One slice of the spending by category.</summary>
/// <param name="CategoryId">The category; null = the bills whose category is missing or deleted.</param>
/// <param name="Amount">Gross of its bills.</param>
public sealed record CategorySlice(Guid? CategoryId, decimal Amount);

/// <summary>What this year cost so far, by category.</summary>
/// <param name="Year">This year.</param>
/// <param name="ThroughMonth">The last month counted: the last month of this year with a bill, may be later than the current month (1–12).</param>
/// <param name="Currency">The currency of the amounts.</param>
/// <param name="Total">Gross of all the bills counted.</param>
/// <param name="Slices">One slice per category with bills, the largest first; bills without a known category form one last-resort slice with a null id.</param>
public sealed record CategorySpending(int Year, int ThroughMonth, Currency Currency, decimal Total, IReadOnlyList<CategorySlice> Slices);

/// <summary>The last twelve complete months of the consumption of one resource of one property.</summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Resource">The resource (cold water, electricity…).</param>
/// <param name="Unit">The unit of the quantities.</param>
/// <param name="Values">Twelve values, the oldest month first; null = not known (no reading), never an invented zero.</param>
/// <param name="Approximate">Twelve flags: the figure rests on estimated or incomplete data.</param>
public sealed record ConsumptionSeries(Guid PropertyId, MeterResource Resource, MeterUnit Unit, IReadOnlyList<decimal?> Values, IReadOnlyList<bool> Approximate);

/// <summary>One row of the "Consumption" card: the last complete month against the average of the eleven months before.</summary>
/// <param name="Resource">The resource.</param>
/// <param name="Unit">The unit.</param>
/// <param name="Values">Twelve monthly values for the small line, oldest first; null = a gap.</param>
/// <param name="Approximate">Twelve flags, like <see cref="ConsumptionSeries.Approximate"/>.</param>
/// <param name="Last">The latest month with a figure (normally the last complete month); null when no month has one.</param>
/// <param name="LastApproximate">Whether <paramref name="Last"/> rests on incomplete data.</param>
/// <param name="ChangePercent">The last month against the average of the eleven before it, whole percent; null if either is missing or the average is zero.</param>
/// <param name="IsRise">The rise is noticeable (above <see cref="OverviewCharts.RisePercent"/>).</param>
/// <param name="ShowLine">Whether there are enough months (<see cref="OverviewCharts.MinMonthsForLine"/>) to draw the line.</param>
/// <param name="LastIndex">Index of <paramref name="Last"/> in <paramref name="Values"/> (11 = the last complete month; smaller when
/// that month has no readings and an older month is shown, with its name); -1 when there is none.</param>
public sealed record ConsumptionTrend(
    MeterResource Resource,
    MeterUnit Unit,
    IReadOnlyList<decimal?> Values,
    IReadOnlyList<bool> Approximate,
    decimal? Last,
    bool LastApproximate,
    decimal? ChangePercent,
    bool IsRise,
    bool ShowLine,
    int LastIndex = -1);

/// <summary>The figures of the three charts of the Overview (T-038). Pure functions over <see cref="OverviewData"/>.</summary>
public static class OverviewCharts
{
    /// <summary>A last month more than this many percent above the average of the eleven before it is marked as a rise.</summary>
    public const decimal RisePercent = 30m;

    /// <summary>Months with a figure needed to draw the small line; with fewer only the number is shown.</summary>
    public const int MinMonthsForLine = 3;

    /// <summary>The months of the consumption line, the last complete one included.</summary>
    public const int ConsumptionMonths = 12;

    /// <summary>
    /// How many months of readings the consumption figures need: the twelve months plus the month before the first one, whose
    /// last reading starts it.
    /// </summary>
    public const int ReadingMonths = ConsumptionMonths + 2;

    /// <summary>
    /// Gross of the bills by bill month for this year and the year before, in the currency most bills are in. Null if there is no
    /// bill this year. Later months of this year show their bar once a bill is entered for them (marked approximate, like a draft month); later months without bills are empty (T-054).
    /// </summary>
    /// <param name="data">The loaded data.</param>
    /// <param name="today">Today.</param>
    /// <param name="propertyId">Only this property; null = all.</param>
    public static MonthlyBars? MonthlyComparison(OverviewData data, DateOnly today, Guid? propertyId)
    {
        ArgumentNullException.ThrowIfNull(data);

        var bills = CountedBills(data, propertyId).Where(b => b.Period.Year == today.Year || b.Period.Year == today.Year - 1).ToList();
        if (!bills.Any(b => b.Period.Year == today.Year))
        {
            return null;
        }

        var currency = PrimaryCurrency(bills);
        bills = [.. bills.Where(b => b.Currency == currency)];
        var drafts = data.Drafts.Where(d => !d.IsDeleted && Visible(data, d.PropertyId, propertyId)).Select(d => d.Period).ToHashSet();

        var (thisYear, thisApprox) = Months(bills, drafts, today.Year, 12, today.Month);
        var hasLastYear = bills.Any(b => b.Period.Year == today.Year - 1);
        var (lastYear, lastApprox) = hasLastYear ? Months(bills, drafts, today.Year - 1, 12, 12) : (new decimal?[12], new bool[12]);
        return new MonthlyBars(today.Year, currency, thisYear, thisApprox, lastYear, lastApprox, hasLastYear);
    }

    private static (decimal?[] Values, bool[] Approximate) Months(List<Bill> bills, HashSet<YearMonth> drafts, int year, int lastMonth, int currentMonth)
    {
        var values = new decimal?[12];
        var approximate = new bool[12];
        for (var month = 1; month <= lastMonth; month++)
        {
            var period = new YearMonth(year, month);
            var ofMonth = bills.Where(b => b.Period == period).ToList();
            values[month - 1] = ofMonth.Count == 0 ? null : ofMonth.Sum(b => b.Gross);
            approximate[month - 1] = drafts.Contains(period) || (month > currentMonth && values[month - 1] is not null);
        }

        return (values, approximate);
    }

    /// <summary>
    /// Gross of the bills from January to the last month that has a bill (bills entered for later months of this year count) by the bill's category, largest first: one slice
    /// per category, plus one slice (null id) for bills whose category is missing or deleted. Null if there is no bill.
    /// </summary>
    /// <param name="data">The loaded data.</param>
    /// <param name="today">Today.</param>
    /// <param name="propertyId">Only this property; null = all.</param>
    public static CategorySpending? SpendingByCategory(OverviewData data, DateOnly today, Guid? propertyId)
    {
        ArgumentNullException.ThrowIfNull(data);

        var bills = CountedBills(data, propertyId).Where(b => b.Period.Year == today.Year).ToList();
        if (bills.Count == 0)
        {
            return null;
        }

        var currency = PrimaryCurrency(bills);
        bills = [.. bills.Where(b => b.Currency == currency)];
        var throughMonth = bills.Max(b => b.Period.Month);   // stop at the last month with a bill (it may be after the current month)
        var known = data.Categories.Where(c => !c.IsDeleted).Select(c => c.Id).ToHashSet();
        var groups = bills
            .GroupBy(b => known.Contains(b.CategoryId) ? (Guid?)b.CategoryId : null)
            .Select(g => new CategorySlice(g.Key, g.Sum(b => b.Gross)))
            .Where(s => s.Amount != 0)
            .OrderByDescending(s => s.Amount)
            .ThenBy(s => s.CategoryId)
            .ToList();
        if (groups.Count == 0)
        {
            return null;
        }

        return new CategorySpending(today.Year, throughMonth, currency, bills.Sum(b => b.Gross), groups);
    }

    /// <summary>
    /// The monthly consumption of the last twelve complete months per property and resource, from the readings (the report
    /// engine: the reading window of the contract, replaced meters as one). Day and night registers count as their total; export
    /// registers are left out. Properties and resources without any figure are not listed.
    /// </summary>
    /// <param name="data">The loaded data; the readings must reach back <see cref="ReadingMonths"/> months.</param>
    /// <param name="today">Today.</param>
    public static IReadOnlyList<ConsumptionSeries> Consumption(OverviewData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Properties.Count == 0 || data.Meters.Count == 0)
        {
            return [];
        }

        var last = YearMonth.From(today).AddMonths(-1);
        var first = last.AddMonths(1 - ConsumptionMonths);
        var reports = new ReportData(data.Properties, [], data.Contracts, data.ContractMeters, data.Meters, [], [], []);
        var filter = new ReportFilter(first, last, [.. data.Properties.Select(p => p.Id)]);
        var table = ReportBuilder.Consumption(reports, filter);

        var result = new List<ConsumptionSeries>();
        for (var column = 0; column < table.Columns.Count; column++)
        {
            var definition = table.Columns[column];
            if (definition is not { Subject: ColumnSubject.Resource, Register: RegisterKind.Total, Resource: { } resource, Unit: { } unit })
            {
                continue;
            }

            var values = new decimal?[ConsumptionMonths];
            var approximate = new bool[ConsumptionMonths];
            foreach (var row in table.Rows.Where(r => r.Kind == ReportRowKind.Month))
            {
                var index = first.MonthsUntil(new YearMonth(row.Year, row.Month));
                if (index is >= 0 and < ConsumptionMonths)
                {
                    values[index] = row.Cells[column].Value;
                    approximate[index] = row.Cells[column].IsApproximate;
                }
            }

            if (values.Any(v => v is not null))
            {
                result.Add(new ConsumptionSeries(definition.PropertyId, resource, unit, values, approximate));
            }
        }

        return result;
    }

    /// <summary>
    /// The rows of the Consumption card for the chosen properties: the series of one resource and unit are added up month by month
    /// (a month stays a gap when no property has a figure for it). Rows in the order of the resources.
    /// </summary>
    /// <param name="series">The result of <see cref="Consumption"/>.</param>
    /// <param name="propertyId">Only this property; null = all.</param>
    public static IReadOnlyList<ConsumptionTrend> Trends(IReadOnlyList<ConsumptionSeries> series, Guid? propertyId)
    {
        ArgumentNullException.ThrowIfNull(series);

        var rows = new List<ConsumptionTrend>();
        foreach (var group in series.Where(s => propertyId is null || s.PropertyId == propertyId).GroupBy(s => (s.Resource, s.Unit)).OrderBy(g => g.Key.Resource).ThenBy(g => g.Key.Unit))
        {
            var values = new decimal?[ConsumptionMonths];
            var approximate = new bool[ConsumptionMonths];
            for (var month = 0; month < ConsumptionMonths; month++)
            {
                var known = group.Where(s => s.Values[month] is not null).ToList();
                values[month] = known.Count == 0 ? null : known.Sum(s => s.Values[month]);
                approximate[month] = group.Any(s => s.Approximate[month]);
            }

            rows.Add(Trend(group.Key.Resource, group.Key.Unit, values, approximate));
        }

        return rows;
    }

    /// <summary>
    /// The row for twelve monthly values: the last month, the change against the average of the eleven months before it (months
    /// without a figure are left out of the average), the rise mark and whether there are enough months for a line.
    /// </summary>
    public static ConsumptionTrend Trend(MeterResource resource, MeterUnit unit, IReadOnlyList<decimal?> values, IReadOnlyList<bool> approximate)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(approximate);

        // The latest month with a figure: the last complete month often has no readings yet (owner's test, T-038: "Газ —").
        var lastIndex = -1;
        for (var i = values.Count - 1; i >= 0; i--)
        {
            if (values[i] is not null)
            {
                lastIndex = i;
                break;
            }
        }

        var last = lastIndex >= 0 ? values[lastIndex] : null;
        var before = values.Take(Math.Max(lastIndex, 0)).OfType<decimal>().ToList();
        decimal? change = null;
        if (last is { } latest && before.Count > 0 && before.Average() is var average and > 0)
        {
            change = Math.Round((latest - average) / average * 100m, 0, MidpointRounding.AwayFromZero);
        }

        return new ConsumptionTrend(
            resource,
            unit,
            values,
            approximate,
            last,
            lastIndex >= 0 && approximate[lastIndex],
            change,
            change > RisePercent,
            values.Count(v => v is not null) >= MinMonthsForLine,
            lastIndex);
    }

    private static IEnumerable<Bill> CountedBills(OverviewData data, Guid? propertyId) =>
        data.Bills.Where(b => !b.IsDeleted && !b.IsDraft && Visible(data, b.PropertyId, propertyId));

    private static bool Visible(OverviewData data, Guid billProperty, Guid? propertyId) =>
        (propertyId is null || billProperty == propertyId) && data.Properties.Any(p => p.Id == billProperty && !p.IsDeleted);

    /// <summary>The currency with the most bills (ties: the smaller enum value, so EUR before the historical LVL).</summary>
    private static Currency PrimaryCurrency(IEnumerable<Bill> bills) =>
        bills.GroupBy(b => b.Currency).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
}
