using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>Where a tap on a cell leads: the bills or readings behind the number.</summary>
/// <param name="Target">Bills or readings.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="From">First month.</param>
/// <param name="To">Last month.</param>
/// <param name="CategoryId">Only this category (bills).</param>
/// <param name="ContractId">Only this contract (bills).</param>
public sealed record ReportDrill(DrillTarget Target, Guid PropertyId, YearMonth From, YearMonth To, Guid? CategoryId = null, Guid? ContractId = null);

/// <summary>One number of a report.</summary>
/// <param name="Value">The number; null = nothing to show (no data, or incomplete data with no number).</param>
/// <param name="IsApproximate">True if the number rests on incomplete data (shown with "≈"): a missing reading, an estimated reading, a calculated amount without a bill, an amount spread over months.</param>
/// <param name="Drill">What a tap opens; null = nothing.</param>
public sealed record ReportCell(decimal? Value, bool IsApproximate = false, ReportDrill? Drill = null)
{
    /// <summary>A cell without a number and without a mark.</summary>
    public static ReportCell Empty { get; } = new((decimal?)null);

    /// <summary>True if there is nothing to show: no number and no mark.</summary>
    public bool IsEmpty => Value is null && !IsApproximate;
}

/// <summary>A column of a report. The names are data (property, contract title…); the UI builds the label from the rest.</summary>
/// <param name="Key">Stable key of the column.</param>
/// <param name="PropertyId">The property the column belongs to; the columns of one property sit together.</param>
/// <param name="PropertyName">Name of that property.</param>
/// <param name="Subject">What the column is about.</param>
/// <param name="Name">Category name, contract title or meter name; null when the UI should use the localized name of <paramref name="Service"/>/<paramref name="Resource"/>.</param>
/// <param name="Service">The service (service columns, built-in categories).</param>
/// <param name="Resource">The resource (consumption columns).</param>
/// <param name="Unit">The unit of the quantity (consumption and unit cost columns).</param>
/// <param name="Register">The register kind (consumption columns).</param>
/// <param name="Measure">The figure shown.</param>
/// <param name="ValueKind">The kind of number.</param>
/// <param name="Year">Year-over-year: the year of the column.</param>
/// <param name="IsSubtotal">True for the total of a property's columns.</param>
/// <param name="CategoryId">The category of a category column (its colour is used in charts).</param>
public sealed record ReportColumn(
    string Key,
    Guid PropertyId,
    string PropertyName,
    ColumnSubject Subject,
    string? Name,
    ServiceKind? Service,
    MeterResource? Resource,
    MeterUnit? Unit,
    RegisterKind? Register,
    ReportMeasure Measure,
    ReportValueKind ValueKind,
    int? Year = null,
    bool IsSubtotal = false,
    Guid? CategoryId = null);

/// <summary>A row of a report.</summary>
/// <param name="Kind">Month, year, month of the year or totals.</param>
/// <param name="Year">The year (0 for month-of-year rows and the totals row).</param>
/// <param name="Month">The month 1–12 (0 for year rows and the totals row).</param>
/// <param name="Cells">One cell per column.</param>
/// <param name="Total">The total of the row across the columns; null for reports where that is meaningless.</param>
public sealed record ReportRow(ReportRowKind Kind, int Year, int Month, IReadOnlyList<ReportCell> Cells, ReportCell? Total);

/// <summary>The result of a report: columns, rows and a totals row.</summary>
/// <param name="Kind">The report.</param>
/// <param name="Filter">What it was calculated for.</param>
/// <param name="Columns">The columns, the columns of a property together.</param>
/// <param name="Rows">The rows, oldest first.</param>
/// <param name="Totals">The totals over the whole range (per year column in year over year); null if there are no columns.</param>
/// <param name="HasRowTotals">True if every row has a total across the columns.</param>
public sealed record ReportTable(
    ReportKind Kind,
    ReportFilter Filter,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    ReportRow? Totals,
    bool HasRowTotals)
{
    /// <summary>True if no column has any data.</summary>
    public bool IsEmpty => Columns.Count == 0;
}

/// <summary>Decimals of the numbers of a report, shared by the screen and the export.</summary>
public static class ReportFormat
{
    /// <summary>2 for money, 3 for quantities, 1 for percent; unit prices have 2 decimals from 1 up and 4 below.</summary>
    public static int Decimals(ReportValueKind kind, decimal value) => kind switch
    {
        ReportValueKind.Money => 2,
        ReportValueKind.Quantity => 3,
        ReportValueKind.Percent => 1,
        _ => Math.Abs(value) >= 1m ? 2 : 4,
    };

    /// <summary>Rounds a unit price: 2 decimals from 1 up, 4 below.</summary>
    public static decimal RoundUnitPrice(decimal value) =>
        Math.Round(value, Math.Abs(value) >= 1m ? 2 : 4, MidpointRounding.AwayFromZero);
}
