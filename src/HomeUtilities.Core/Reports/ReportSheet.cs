using System.Globalization;
using System.Text;

namespace HomeUtilities.Core.Reports;

/// <summary>What a cell of an exported report holds.</summary>
public enum SheetCellKind
{
    /// <summary>Nothing.</summary>
    Empty = 0,

    /// <summary>Text.</summary>
    Text = 1,

    /// <summary>A number with a number of decimals.</summary>
    Number = 2,

    /// <summary>A date (shown as year and month).</summary>
    Month = 3,
}

/// <summary>A cell of an exported report.</summary>
/// <param name="Kind">What it holds.</param>
/// <param name="Text">The text (kind Text; for a number without a value that is approximate: "≈").</param>
/// <param name="Number">The number (kind Number).</param>
/// <param name="Month">The first day of the month (kind Month).</param>
/// <param name="Decimals">Decimals of the number.</param>
/// <param name="IsApproximate">True if the number rests on incomplete data.</param>
/// <param name="IsBold">True for totals and headings.</param>
public readonly record struct SheetCell(
    SheetCellKind Kind,
    string? Text = null,
    decimal? Number = null,
    DateOnly? Month = null,
    int Decimals = 0,
    bool IsApproximate = false,
    bool IsBold = false)
{
    /// <summary>An empty cell.</summary>
    public static SheetCell Empty => default;

    /// <summary>A text cell.</summary>
    public static SheetCell FromText(string text, bool bold = false) => new(SheetCellKind.Text, Text: text, IsBold: bold);

    /// <summary>A number cell.</summary>
    public static SheetCell FromNumber(decimal value, int decimals, bool approximate = false, bool bold = false) =>
        new(SheetCellKind.Number, Number: value, Decimals: decimals, IsApproximate: approximate, IsBold: bold);

    /// <summary>A month cell.</summary>
    public static SheetCell FromMonth(DateOnly firstDay, bool bold = false) => new(SheetCellKind.Month, Month: firstDay, IsBold: bold);
}

/// <summary>
/// A report as it is exported: a title, the filters as text, header rows, rows of cells and a legend. Built from a
/// <see cref="ReportTable"/> with the localized texts; the writers (Excel, CSV) only lay it out.
/// </summary>
/// <param name="Title">The report's name.</param>
/// <param name="Filters">Label and value of each filter, shown above the table.</param>
/// <param name="HeaderRows">The header rows (property group, column label…), as long as the data rows.</param>
/// <param name="Rows">The data rows, the totals row last.</param>
/// <param name="Legend">The explanation of the marks; null = none.</param>
public sealed record ReportSheet(
    string Title,
    IReadOnlyList<(string Label, string Value)> Filters,
    IReadOnlyList<IReadOnlyList<string>> HeaderRows,
    IReadOnlyList<IReadOnlyList<SheetCell>> Rows,
    string? Legend);

/// <summary>Writes a <see cref="ReportSheet"/> as a workbook (implemented with ClosedXML in HomeUtilities.Import).</summary>
public interface IReportWorkbookWriter
{
    /// <summary>Writes an .xlsx file with one sheet.</summary>
    void Write(ReportSheet sheet, Stream stream);
}

/// <summary>Writes a <see cref="ReportSheet"/> as CSV: UTF-8 with a byte order mark and ";" as the separator (Latvian and Russian Excel).</summary>
public static class ReportCsvWriter
{
    /// <summary>The separator.</summary>
    public const char Separator = ';';

    /// <summary>Writes the CSV. Numbers use the decimal separator of <paramref name="culture"/> and no thousands separator; approximate numbers get a leading "≈".</summary>
    public static void Write(ReportSheet sheet, Stream stream, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(culture);

        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true) { NewLine = "\r\n" };
        writer.WriteLine(Quote(sheet.Title));
        foreach (var (label, value) in sheet.Filters)
        {
            writer.WriteLine(Quote(label) + Separator + Quote(value));
        }

        writer.WriteLine();
        foreach (var header in sheet.HeaderRows)
        {
            writer.WriteLine(string.Join(Separator, header.Select(Quote)));
        }

        foreach (var row in sheet.Rows)
        {
            writer.WriteLine(string.Join(Separator, row.Select(c => Format(c, culture))));
        }

        if (sheet.Legend is { Length: > 0 } legend)
        {
            writer.WriteLine();
            writer.WriteLine(Quote(legend));
        }

        writer.Flush();
    }

    private static string Format(SheetCell cell, CultureInfo culture) => cell.Kind switch
    {
        SheetCellKind.Text => Quote(cell.Text ?? string.Empty),
        SheetCellKind.Number => (cell.IsApproximate ? "≈ " : string.Empty)
            + cell.Number!.Value.ToString("F" + cell.Decimals.ToString(CultureInfo.InvariantCulture), culture),
        SheetCellKind.Month => cell.Month!.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    private static string Quote(string text) =>
        text.IndexOfAny([Separator, '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
}
