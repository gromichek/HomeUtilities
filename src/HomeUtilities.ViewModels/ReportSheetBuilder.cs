using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels;

/// <summary>Turns the table on the screen into the sheet that is exported: the same numbers, headings and filters.</summary>
public static class ReportSheetBuilder
{
    /// <summary>The file name <c>HomeUtilities-&lt;report&gt;-&lt;from&gt;-&lt;to&gt;.&lt;extension&gt;</c>, e.g. <c>HomeUtilities-costs-2026-01-2026-12.xlsx</c>.</summary>
    public static string FileName(ReportKind kind, ReportFilter filter, string extension)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var report = kind switch
        {
            ReportKind.Costs => "costs",
            ReportKind.Consumption => "consumption",
            ReportKind.UnitCost => "unit-cost",
            _ => "calculated-vs-billed",
        };
        return $"HomeUtilities-{report}-{filter.From}-{filter.To}.{extension}";
    }

    /// <summary>Builds the sheet.</summary>
    /// <param name="table">The report as shown.</param>
    /// <param name="labels">The texts.</param>
    /// <param name="localizer">The localizer for the filter block.</param>
    /// <param name="propertiesText">The selected properties as text ("All properties" or the names).</param>
    /// <param name="categoriesText">The selected categories as text; null when the report has no category filter.</param>
    /// <param name="createdOn">Today.</param>
    public static ReportSheet Build(
        ReportTable table,
        ReportLabels labels,
        ILocalizer localizer,
        string propertiesText,
        string? categoriesText,
        DateOnly createdOn)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(localizer);

        var filter = table.Filter;
        var filters = new List<(string, string)>
        {
            (localizer["ReportSheetReport"], labels.KindName(table.Kind)),
            (localizer["ReportSheetPeriod"], $"{filter.From} – {filter.To}"),
            (localizer["ReportPropertiesLabel"], propertiesText),
        };
        if (categoriesText is not null)
        {
            filters.Add((localizer["ReportCategoriesLabel"], categoriesText));
        }

        if (table.Kind == ReportKind.Costs)
        {
            filters.Add((localizer["ReportGroupingLabel"], localizer["ReportGroup" + filter.Grouping]));
        }

        if (table.Kind != ReportKind.Consumption)
        {
            filters.Add((localizer["ReportMeasureLabel"], labels.MeasureName(table.Columns.Count > 0 ? table.Columns[0].Measure : filter.Measure)));
        }

        filters.Add((localizer["ReportRowsLabel"], filter.YearOverYear
            ? localizer["ReportYearOverYear"]
            : localizer[filter.Rows == ReportRows.Years ? "ReportRowsYears" : "ReportRowsMonths"]));
        filters.Add((localizer["ReportSheetCreated"], createdOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        var groups = new List<string> { string.Empty };
        var headers = new List<string> { labels.PeriodHeader };
        foreach (var column in table.Columns)
        {
            groups.Add(column.PropertyName);
            headers.Add(labels.Column(column));
        }

        if (table.HasRowTotals)
        {
            groups.Add(string.Empty);
            headers.Add(labels.Total);
        }

        var rows = new List<IReadOnlyList<SheetCell>>();
        foreach (var row in table.Rows.Append(table.Totals).OfType<ReportRow>())
        {
            var totals = row.Kind == ReportRowKind.Total;
            var cells = new List<SheetCell> { LabelCell(row, labels) };
            for (var i = 0; i < row.Cells.Count; i++)
            {
                cells.Add(ValueCell(row.Cells[i], table.Columns[i], totals || table.Columns[i].IsSubtotal));
            }

            if (table.HasRowTotals && row.Total is { } total)
            {
                cells.Add(ValueCell(total, table.Columns[0], true));
            }

            rows.Add(cells);
        }

        return new ReportSheet(
            labels.KindName(table.Kind),
            filters,
            table.IsEmpty ? [[labels.PeriodHeader]] : [groups, headers],
            table.IsEmpty ? [] : rows,
            localizer["ReportLegend"]);
    }

    private static SheetCell LabelCell(ReportRow row, ReportLabels labels) => row.Kind switch
    {
        ReportRowKind.Month => SheetCell.FromMonth(new YearMonth(row.Year, row.Month).FirstDay),
        ReportRowKind.Total => SheetCell.FromText(labels.Row(row), bold: true),
        _ => SheetCell.FromText(labels.Row(row)),
    };

    private static SheetCell ValueCell(ReportCell cell, ReportColumn column, bool bold)
    {
        if (cell.Value is not { } value)
        {
            return new SheetCell(SheetCellKind.Empty, IsApproximate: cell.IsApproximate, IsBold: bold);
        }

        return SheetCell.FromNumber(value, ReportFormat.Decimals(column.ValueKind, value), cell.IsApproximate, bold);
    }
}
