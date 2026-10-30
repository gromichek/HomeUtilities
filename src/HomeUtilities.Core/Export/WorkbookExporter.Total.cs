using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Export;

public static partial class WorkbookExporter
{
    // Rows = months, columns = contracts, in EUR: the numbers of Reports → Costs, grouped by contract.
    private static ExportSheet? TotalSheet(WorkbookExportInput input, WorkbookExportOptions options, ExportLabels labels, string name, IReadOnlyList<Contract> contracts)
    {
        var data = input.Data;
        var first = options.From ?? data.EarliestMonth;
        var last = options.To ?? data.LatestMonth;
        if (first is not { } from || last is not { } to || to < from)
        {
            return null;
        }

        var selected = contracts.Select(c => c.Id).ToHashSet();
        var filter = new ReportFilter(
            from,
            to,
            PropertyIds: [.. contracts.Select(c => c.PropertyId).Distinct()],
            Rows: ReportRows.Months,
            Grouping: CostGrouping.Contract,
            Measure: ReportMeasure.Gross);
        var table = ReportBuilder.Costs(data, filter);

        var wantOneOff = options.ContractIds is null;
        var columns = new List<(int Index, ReportColumn Column)>();
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var parts = column.Key.Split('|');
            var keep = !column.IsSubtotal && parts.Length >= 2
                && ((parts[1] == "contract" && parts.Length == 3 && Guid.TryParse(parts[2], out var id) && selected.Contains(id)) || (parts[1] == "oneoff" && wantOneOff));
            if (keep)
            {
                columns.Add((index, column));
            }
        }

        if (columns.Count == 0)
        {
            return null;
        }

        var multiple = columns.Select(c => c.Column.PropertyId).Distinct().Count() > 1;
        var cells = new List<PlacedCell>();
        cells.Add(new PlacedCell(1, 1, ExportCell.FromText($"{labels.TotalMonth} (EUR)", bold: true)));
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i].Column;
            var text = column.Name ?? labels.OneOff;
            cells.Add(new PlacedCell(1, i + 2, ExportCell.FromText(multiple ? $"{column.PropertyName} – {text}" : text, bold: true)));
        }

        var totalColumn = columns.Count + 2;
        cells.Add(new PlacedCell(1, totalColumn, ExportCell.FromText(labels.TotalColumn, bold: true)));

        var columnTotals = new decimal[columns.Count];
        var line = 2;
        foreach (var row in table.Rows.Where(r => r.Kind == ReportRowKind.Month))
        {
            cells.Add(new PlacedCell(line, 1, ExportCell.FromMonth(new DateOnly(row.Year, row.Month, 1))));
            var rowTotal = 0m;
            for (var i = 0; i < columns.Count; i++)
            {
                if (row.Cells[columns[i].Index].Value is { } value)
                {
                    cells.Add(new PlacedCell(line, i + 2, ExportCell.FromNumber(value, 2)));
                    rowTotal += value;
                    columnTotals[i] += value;
                }
            }

            cells.Add(new PlacedCell(line, totalColumn, ExportCell.FromNumber(rowTotal, 2, bold: true)));
            line++;
        }

        cells.Add(new PlacedCell(line, 1, ExportCell.FromText(labels.TotalColumn, bold: true)));
        for (var i = 0; i < columns.Count; i++)
        {
            cells.Add(new PlacedCell(line, i + 2, ExportCell.FromNumber(columnTotals[i], 2, bold: true)));
        }

        cells.Add(new PlacedCell(line, totalColumn, ExportCell.FromNumber(columnTotals.Sum(), 2, bold: true)));
        return new ExportSheet(name, cells, 1, 1);
    }
}
