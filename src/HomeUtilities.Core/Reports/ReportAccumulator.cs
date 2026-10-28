using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Reports;

/// <summary>The raw figures of one column and one month (or a sum of months); each report reads A, B and C its own way.</summary>
internal struct Acc
{
    /// <summary>First figure (costs: amount; consumption: quantity; unit cost: money; calculated vs billed: calculated).</summary>
    public decimal A;

    /// <summary>Second figure (unit cost: quantity; calculated vs billed: billed).</summary>
    public decimal B;

    /// <summary>Third figure (calculated vs billed: quantity difference).</summary>
    public decimal C;

    /// <summary>True if at least one month contributed figures.</summary>
    public bool Any;

    /// <summary>True if <see cref="C"/> was filled.</summary>
    public bool HasC;

    /// <summary>True if the figures are approximate (spread, estimated, calculated instead of billed).</summary>
    public bool Approx;

    /// <summary>True if at least one month could not be determined.</summary>
    public bool Missing;

    public static Acc operator +(Acc left, Acc right) => new()
    {
        A = left.A + right.A,
        B = left.B + right.B,
        C = left.C + right.C,
        Any = left.Any || right.Any,
        HasC = left.HasC || right.HasC,
        Approx = left.Approx || right.Approx,
        Missing = left.Missing || right.Missing,
    };

    public readonly bool IsEmpty => !Any && !Missing;
}

/// <summary>A report before it is laid out: the columns and the figures per column and month.</summary>
internal sealed class ReportModel(ReportKind kind, ReportFilter filter)
{
    public ReportKind Kind { get; } = kind;

    public ReportFilter Filter { get; } = filter;

    public List<ReportColumn> Columns { get; } = [];

    public List<Dictionary<YearMonth, Acc>> Cells { get; } = [];

    /// <summary>The drill target of the column; null = no drill.</summary>
    public List<Func<YearMonth, YearMonth, ReportDrill?>> Drills { get; } = [];

    /// <summary>Turns the figures of a column into a cell.</summary>
    public required Func<ReportColumn, Acc, ReportCell> ToCell { get; init; }

    /// <summary>True if the cells of a row can be added up into a row total.</summary>
    public bool HasRowTotals { get; init; }

    public void Add(ReportColumn column, Dictionary<YearMonth, Acc> cells, Func<YearMonth, YearMonth, ReportDrill?> drill)
    {
        Columns.Add(column);
        Cells.Add(cells);
        Drills.Add(drill);
    }
}
