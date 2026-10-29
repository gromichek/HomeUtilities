using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Turns a <see cref="ReportTable"/> into charts: the same numbers as the table, one chart per unit (m³, kWh…). Costs are bars
/// (stacked when the columns are parts of a whole), consumption, unit cost and quantity differences are lines, differences of
/// money are bars around zero, year over year has one series per year over the months 1–12.
/// </summary>
public static class ReportChartBuilder
{
    /// <summary>The most series in one chart; the rest of an additive chart is summed as "Other".</summary>
    public const int MaxSeries = 8;

    /// <summary>The colour of "Other".</summary>
    public const string OtherColor = "#9E9E9E";

    /// <summary>Colours for series without a category colour (readable on a light and a dark background).</summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#4E79A7", "#F28E2B", "#59A14F", "#E15759", "#B07AA1", "#76B7B2", "#EDC948", "#9C755F",
    ];

    private sealed record Raw(string Key, string Name, string? Color, decimal?[] Values, bool[] Approx);

    /// <summary>Builds the charts of a table; empty if there is nothing to draw.</summary>
    /// <param name="table">The report.</param>
    /// <param name="labels">The texts of the table.</param>
    /// <param name="localizer">The localizer for "Other" and the note about approximate points.</param>
    /// <param name="categoryColors">Colour of each category, by id.</param>
    /// <param name="shownByYear">True if <paramref name="table"/> has years as rows although the table on the screen has months (a long period):
    /// the charts then say so.</param>
    public static IReadOnlyList<ChartModel> Build(
        ReportTable table,
        ReportLabels labels,
        ILocalizer localizer,
        IReadOnlyDictionary<Guid, string> categoryColors,
        bool shownByYear = false)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(categoryColors);

        if (table.IsEmpty)
        {
            return [];
        }

        var rows = table.Rows.Where(r => r.Kind != ReportRowKind.Total).ToList();
        var periods = rows.Select(labels.ChartRow).ToList();
        var charts = new List<ChartModel>();
        var withoutSubtotals = Enumerable.Range(0, table.Columns.Count).Where(i => !table.Columns[i].IsSubtotal).ToList();
        var multipleProperties = withoutSubtotals.Select(i => table.Columns[i].PropertyId).Distinct().Count() > 1;

        foreach (var group in withoutSubtotals.GroupBy(i => table.Columns[i].Unit).OrderBy(g => g.Key))
        {
            var indexes = group.ToList();
            var first = table.Columns[indexes[0]];
            var raws = table.Filter.YearOverYear
                ? ByYear(table, rows, indexes)
                : ByColumn(table, rows, indexes, labels, categoryColors, multipleProperties);
            if (!raws.Any(r => r.Values.Any(v => v is not null)))
            {
                continue;
            }

            // Money and quantities can be summed into "Other"; unit prices and percentages cannot.
            raws = Limit(raws, localizer, first.ValueKind is ReportValueKind.Money or ReportValueKind.Quantity);

            var kind = ChartKindOf(table, first);
            var unit = UnitText(first, labels, localizer);
            var series = Colour(raws).Select(r => new ChartSeries(
                r.Name,
                r.Color!,
                [.. rows.Select((row, i) => new ChartPoint(r.Values[i], r.Approx[i], Tooltip(r, i, periods[i], first.ValueKind, unit, labels)))])).ToList();
            var approximate = series.Any(s => s.Points.Any(p => p.IsApproximate));
            charts.Add(new ChartModel(
                Title(table, first, labels, localizer),
                kind,
                periods,
                series,
                first.ValueKind,
                unit,
                approximate,
                approximate ? localizer["ReportChartApproximate"] : null,
                shownByYear ? localizer["ReportChartByYear"] : null));
        }

        return charts;
    }

    private static ChartKind ChartKindOf(ReportTable table, ReportColumn first) => table.Kind switch
    {
        ReportKind.Costs => !table.Filter.YearOverYear && table.Filter.Grouping != CostGrouping.Property ? ChartKind.StackedBars : ChartKind.Bars,
        ReportKind.CalculatedVsBilled => first.ValueKind == ReportValueKind.Quantity ? ChartKind.Lines : ChartKind.Bars,
        _ => ChartKind.Lines,
    };

    private static List<Raw> ByColumn(
        ReportTable table,
        IReadOnlyList<ReportRow> rows,
        IReadOnlyList<int> indexes,
        ReportLabels labels,
        IReadOnlyDictionary<Guid, string> categoryColors,
        bool multipleProperties)
    {
        var raws = new List<Raw>();
        foreach (var index in indexes)
        {
            var column = table.Columns[index];
            var key = SeriesKey(table, column);
            var values = new decimal?[rows.Count];
            var approx = new bool[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                values[i] = rows[i].Cells[index].Value;
                approx[i] = rows[i].Cells[index].IsApproximate;
            }

            var existing = raws.FindIndex(r => r.Key == key);
            if (existing >= 0)
            {
                // The same category, service or "one-off" of another property: one series with the sum.
                raws[existing] = Add(raws[existing], values, approx);
                continue;
            }

            var merged = key != column.Key;
            var color = column.CategoryId is { } category && categoryColors.TryGetValue(category, out var known) ? known : null;

            // Consumption by contract: two contracts of one category must stay distinguishable, so the second one takes a palette colour.
            if (table.Kind == ReportKind.Consumption && color is not null && raws.Any(r => r.Color == color))
            {
                color = null;
            }

            raws.Add(new Raw(key, labels.SeriesName(column, multipleProperties && !merged), color, values, approx));
        }

        return raws;
    }

    private static List<Raw> ByYear(ReportTable table, IReadOnlyList<ReportRow> rows, IReadOnlyList<int> indexes)
    {
        // The columns of a year are summed (the day and night registers are parts of the total: only the total counts).
        var raws = new List<Raw>();
        foreach (var year in indexes.Select(i => table.Columns[i].Year).Where(y => y is not null).Select(y => y!.Value).Distinct().Order())
        {
            var parts = indexes.Where(i => table.Columns[i].Year == year && table.Columns[i].Register is null or RegisterKind.Total).ToList();
            var values = new decimal?[rows.Count];
            var approx = new bool[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                var cells = parts.Select(p => rows[i].Cells[p]).ToList();
                values[i] = cells.Any(c => c.Value is not null) ? cells.Sum(c => c.Value ?? 0m) : null;
                approx[i] = cells.Any(c => c.IsApproximate);
            }

            raws.Add(new Raw(year.ToString(CultureInfo.InvariantCulture), year.ToString(CultureInfo.InvariantCulture), null, values, approx));
        }

        return raws;
    }

    private static string SeriesKey(ReportTable table, ReportColumn column)
    {
        if (table.Kind != ReportKind.Costs)
        {
            return column.Key;
        }

        return column.Subject switch
        {
            ColumnSubject.Category when column.CategoryId is { } category => "category|" + category,
            ColumnSubject.Service => "service|" + column.Service,
            ColumnSubject.OneOff => "oneoff",
            _ => column.Key,
        };
    }

    private static Raw Add(Raw raw, decimal?[] values, bool[] approx)
    {
        var sum = new decimal?[values.Length];
        var flags = new bool[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            sum[i] = raw.Values[i] is null && values[i] is null ? null : (raw.Values[i] ?? 0m) + (values[i] ?? 0m);
            flags[i] = raw.Approx[i] || approx[i];
        }

        return raw with { Values = sum, Approx = flags };
    }

    /// <summary>At most <see cref="MaxSeries"/> series: the largest stay, the rest is summed as "Other" (only where a sum makes sense).</summary>
    private static List<Raw> Limit(List<Raw> raws, ILocalizer localizer, bool canSum)
    {
        if (raws.Count <= MaxSeries)
        {
            return raws;
        }

        var weight = raws.ToDictionary(r => r, r => r.Values.Sum(v => Math.Abs(v ?? 0m)));
        var kept = raws.OrderByDescending(r => weight[r]).Take(canSum ? MaxSeries - 1 : MaxSeries).ToHashSet();
        var result = raws.Where(kept.Contains).ToList();
        if (!canSum)
        {
            return result;
        }

        var other = raws.Where(r => !kept.Contains(r)).Aggregate(
            new Raw("other", localizer["ReportChartOther"], OtherColor, new decimal?[raws[0].Values.Length], new bool[raws[0].Values.Length]),
            (acc, r) => Add(acc, r.Values, r.Approx));
        result.Add(other);
        return result;
    }

    private static IEnumerable<Raw> Colour(List<Raw> raws)
    {
        var next = 0;
        foreach (var raw in raws)
        {
            yield return raw.Color is not null ? raw : raw with { Color = Palette[next++ % Palette.Count] };
        }
    }

    private static string UnitText(ReportColumn column, ReportLabels labels, ILocalizer localizer)
    {
        var euro = localizer["Currency_Symbol_EUR"];
        var unit = column.Unit is { } u ? labels.UnitName(u) : string.Empty;
        return column.ValueKind switch
        {
            ReportValueKind.Money => euro,
            ReportValueKind.UnitPrice => unit.Length > 0 ? $"{euro}/{unit}" : euro,
            ReportValueKind.Percent => "%",
            _ => unit,
        };
    }

    private static string Title(ReportTable table, ReportColumn first, ReportLabels labels, ILocalizer localizer)
    {
        var kind = labels.KindName(table.Kind);
        var unit = UnitText(first, labels, localizer);
        return table.Kind switch
        {
            ReportKind.Consumption => unit.Length > 0 ? $"{kind}, {unit}" : kind,
            _ => unit.Length > 0 && first.ValueKind != ReportValueKind.Percent
                ? $"{kind} · {labels.MeasureName(first.Measure)} ({unit})"
                : $"{kind} · {labels.MeasureName(first.Measure)}",
        };
    }

    private static string Tooltip(Raw series, int index, string period, ReportValueKind kind, string unit, ReportLabels labels)
    {
        if (series.Values[index] is not { } value)
        {
            return $"{series.Name} · {period}";
        }

        var number = labels.Number(value, kind);
        var text = kind == ReportValueKind.Percent || unit.Length == 0 ? number : $"{number} {unit}";
        return $"{series.Name} · {period}: {(series.Approx[index] ? "≈ " : string.Empty)}{text}";
    }
}
