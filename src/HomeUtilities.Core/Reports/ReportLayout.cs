using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Reports;

/// <summary>Lays a <see cref="ReportModel"/> out as rows: one per month or year, or the months of the year across the years.</summary>
internal static class ReportLayout
{
    public static ReportTable Build(ReportModel model)
    {
        var filter = model.Filter;
        if (model.Columns.Count == 0 || filter.To < filter.From)
        {
            return new ReportTable(model.Kind, filter, [], [], null, false);
        }

        return filter.YearOverYear ? YearOverYear(model) : Plain(model);
    }

    private static ReportTable Plain(ReportModel model)
    {
        var filter = model.Filter;
        var months = filter.Months();
        var buckets = filter.Rows == ReportRows.Years
            ? months.GroupBy(m => m.Year).Select(g => (ReportRowKind.Year, g.Key, 0, (IReadOnlyList<YearMonth>)[.. g])).ToList()
            : months.Select(m => (ReportRowKind.Month, m.Year, m.Month, (IReadOnlyList<YearMonth>)[m])).ToList();

        var rows = buckets
            .Select(b => Row(model, b.Item1, b.Item2, b.Item3, b.Item4))
            .ToList();
        var totals = Row(model, ReportRowKind.Total, 0, 0, months);
        return new ReportTable(model.Kind, filter, model.Columns, rows, totals, model.HasRowTotals);
    }

    private static ReportTable YearOverYear(ReportModel model)
    {
        var filter = model.Filter;
        var years = Enumerable.Range(filter.From.Year, filter.To.Year - filter.From.Year + 1).ToList();

        // One column per original column and year; the original columns of a property stay together.
        var columns = new List<(ReportColumn Column, int Source, int Year)>();
        for (var source = 0; source < model.Columns.Count; source++)
        {
            foreach (var year in years)
            {
                columns.Add((model.Columns[source] with { Key = $"{model.Columns[source].Key}|{year}", Year = year }, source, year));
            }
        }

        IReadOnlyList<YearMonth> MonthsOf(int year, int? month) =>
            [.. filter.Months().Where(m => m.Year == year && (month is null || m.Month == month))];

        ReportRow Make(ReportRowKind kind, int month)
        {
            var cells = new List<ReportCell>(columns.Count);
            foreach (var (column, source, year) in columns)
            {
                var included = MonthsOf(year, kind == ReportRowKind.Total ? null : month);
                cells.Add(Cell(model, source, column, included));
            }

            return new ReportRow(kind, 0, kind == ReportRowKind.Total ? 0 : month, cells, null);
        }

        var rows = Enumerable.Range(1, 12).Select(m => Make(ReportRowKind.MonthOfYear, m)).ToList();
        var yearColumns = columns.Select(c => c.Column).ToList();
        return new ReportTable(model.Kind, filter, yearColumns, rows, Make(ReportRowKind.Total, 0), false);
    }

    private static ReportRow Row(ReportModel model, ReportRowKind kind, int year, int month, IReadOnlyList<YearMonth> months)
    {
        var cells = new List<ReportCell>(model.Columns.Count);
        for (var i = 0; i < model.Columns.Count; i++)
        {
            cells.Add(Cell(model, i, model.Columns[i], months));
        }

        ReportCell? total = null;
        if (model.HasRowTotals)
        {
            var parts = cells.Where((_, i) => !model.Columns[i].IsSubtotal).ToList();
            total = new ReportCell(
                parts.Any(c => c.Value is not null) ? parts.Sum(c => c.Value ?? 0m) : null,
                parts.Any(c => c.IsApproximate));
        }

        return new ReportRow(kind, year, month, cells, total);
    }

    private static ReportCell Cell(ReportModel model, int index, ReportColumn column, IReadOnlyList<YearMonth> months)
    {
        var sum = new Acc();
        foreach (var month in months)
        {
            if (model.Cells[index].TryGetValue(month, out var acc))
            {
                sum += acc;
            }
        }

        if (sum.IsEmpty)
        {
            return ReportCell.Empty;
        }

        var cell = model.ToCell(column, sum);
        if (months.Count > 0 && model.Drills[index](months[0], months[^1]) is { } drill)
        {
            cell = cell with { Drill = drill };
        }

        return cell;
    }
}
