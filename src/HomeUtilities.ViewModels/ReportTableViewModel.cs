using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels;

/// <summary>The property name above a run of columns.</summary>
/// <param name="Text">The property name.</param>
/// <param name="Span">How many columns it covers.</param>
/// <param name="Index">0, 1, 2… per property in order (used to tint every other group); -1 for the grand total column.</param>
public sealed record ReportHeaderGroup(string Text, int Span, int Index = -1);

/// <summary>A number of the table as shown.</summary>
public sealed class ReportCellViewModel
{
    /// <summary>Creates the cell; <paramref name="open"/> is the tap action (null = not tappable).</summary>
    public ReportCellViewModel(string text, bool isApproximate, bool isSubtotal, Action? open)
    {
        Text = text;
        IsApproximate = isApproximate;
        IsSubtotal = isSubtotal;
        OpenCommand = open is null ? null : new RelayCommand(open);
    }

    /// <summary>The number or "≈".</summary>
    public string Text { get; }

    /// <summary>True if the number rests on incomplete data (muted).</summary>
    public bool IsApproximate { get; }

    /// <summary>True for the total of a property's columns (bold).</summary>
    public bool IsSubtotal { get; }

    /// <summary>Opens the bills or readings behind the number; null if there are none.</summary>
    public ICommand? OpenCommand { get; }
}

/// <summary>A row of the table.</summary>
/// <param name="Label">The period.</param>
/// <param name="Cells">One cell per column.</param>
/// <param name="Total">The total across the columns; null if the report has none.</param>
/// <param name="IsTotalsRow">True for the last row (bold).</param>
public sealed record ReportRowViewModel(string Label, IReadOnlyList<ReportCellViewModel> Cells, ReportCellViewModel? Total, bool IsTotalsRow);

/// <summary>A report table ready to draw: headings, rows and the totals row.</summary>
/// <param name="PeriodHeader">The heading of the first column.</param>
/// <param name="Groups">The property names above the columns.</param>
/// <param name="ColumnHeaders">The column headings.</param>
/// <param name="TotalHeader">The heading of the row totals column; null if the report has none.</param>
/// <param name="Rows">The rows, the totals row last.</param>
public sealed record ReportTableViewModel(
    string PeriodHeader,
    IReadOnlyList<ReportHeaderGroup> Groups,
    IReadOnlyList<string> ColumnHeaders,
    string? TotalHeader,
    IReadOnlyList<ReportRowViewModel> Rows)
{
    /// <summary>No table.</summary>
    public static ReportTableViewModel Empty { get; } = new(string.Empty, [], [], null, []);

    /// <summary>The number of columns after the first, including the row totals.</summary>
    public int ColumnCount => ColumnHeaders.Count + (TotalHeader is null ? 0 : 1);

    /// <summary>
    /// Builds the view model of a table. <paramref name="open"/> is called with the drill target of a tapped cell.
    /// </summary>
    public static ReportTableViewModel From(ReportTable table, ReportLabels labels, Action<ReportDrill> open)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(open);

        if (table.IsEmpty)
        {
            return Empty;
        }

        var groups = new List<ReportHeaderGroup>();
        var groupIds = new List<Guid>();
        foreach (var column in table.Columns)
        {
            if (groups.Count > 0 && groupIds[^1] == column.PropertyId)
            {
                groups[^1] = groups[^1] with { Span = groups[^1].Span + 1 };
            }
            else
            {
                groups.Add(new ReportHeaderGroup(column.PropertyName, 1, groups.Count));
                groupIds.Add(column.PropertyId);
            }
        }

        if (table.HasRowTotals)
        {
            groups.Add(new ReportHeaderGroup(string.Empty, 1, -1));
        }

        ReportCellViewModel Make(ReportCell cell, ReportColumn column) => new(
            labels.Cell(cell, column),
            cell.IsApproximate,
            column.IsSubtotal,
            cell.Drill is { } drill ? () => open(drill) : null);

        ReportRowViewModel MakeRow(ReportRow row, bool totals)
        {
            var cells = row.Cells.Select((c, i) => Make(c, table.Columns[i])).ToList();
            ReportCellViewModel? total = null;
            if (row.Total is { } sum)
            {
                var kind = table.Columns[0].ValueKind;
                total = new ReportCellViewModel(
                    sum.Value is { } value ? (sum.IsApproximate ? "≈ " : string.Empty) + labels.Number(value, kind) : (sum.IsApproximate ? "≈" : string.Empty),
                    sum.IsApproximate,
                    true,
                    null);
            }

            return new ReportRowViewModel(labels.Row(row), cells, total, totals);
        }

        var rows = table.Rows.Select(r => MakeRow(r, false)).ToList();
        if (table.Totals is { } totalsRow)
        {
            rows.Add(MakeRow(totalsRow, true));
        }

        return new ReportTableViewModel(
            labels.PeriodHeader,
            groups,
            [.. table.Columns.Select(labels.Column)],
            table.HasRowTotals ? labels.Total : null,
            rows);
    }
}
