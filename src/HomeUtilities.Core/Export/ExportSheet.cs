namespace HomeUtilities.Core.Export;

/// <summary>What a cell of the exported workbook holds.</summary>
public enum ExportCellKind
{
    /// <summary>Text.</summary>
    Text = 0,

    /// <summary>A number with a number of decimals.</summary>
    Number = 1,

    /// <summary>A date, shown as day.month.year.</summary>
    Date = 2,

    /// <summary>A month (a date shown as year-month).</summary>
    Month = 3,
}

/// <summary>A value that goes into one cell.</summary>
/// <param name="Kind">What it holds.</param>
/// <param name="Text">The text (kind Text).</param>
/// <param name="Number">The number (kind Number).</param>
/// <param name="Date">The date (kind Date, Month).</param>
/// <param name="Decimals">Decimals of the number.</param>
/// <param name="IsBold">True for headings and totals.</param>
public readonly record struct ExportCell(ExportCellKind Kind, string? Text = null, decimal? Number = null, DateOnly? Date = null, int Decimals = 0, bool IsBold = false)
{
    /// <summary>A text cell.</summary>
    public static ExportCell FromText(string text, bool bold = false) => new(ExportCellKind.Text, Text: text, IsBold: bold);

    /// <summary>A number cell.</summary>
    public static ExportCell FromNumber(decimal value, int decimals, bool bold = false) => new(ExportCellKind.Number, Number: value, Decimals: decimals, IsBold: bold);

    /// <summary>A date cell.</summary>
    public static ExportCell FromDate(DateOnly date) => new(ExportCellKind.Date, Date: date);

    /// <summary>A month cell.</summary>
    public static ExportCell FromMonth(DateOnly firstDay, bool bold = false) => new(ExportCellKind.Month, Date: firstDay, IsBold: bold);
}

/// <summary>A cell value at a position (1-based row and column).</summary>
/// <param name="Row">Row number.</param>
/// <param name="Column">Column number.</param>
/// <param name="Cell">The value.</param>
public readonly record struct PlacedCell(int Row, int Column, ExportCell Cell);

/// <summary>One sheet of the exported workbook; the cells are placed explicitly so that a sheet can follow the layout of an import profile.</summary>
/// <param name="Name">Sheet name (valid for Excel and unique in the workbook).</param>
/// <param name="Cells">The cells that hold something.</param>
/// <param name="HeaderRow">Row of the column headers (bold, frozen); 0 = none.</param>
/// <param name="FrozenColumns">Number of columns kept in view when scrolling; 0 = none.</param>
public sealed record ExportSheet(string Name, IReadOnlyList<PlacedCell> Cells, int HeaderRow, int FrozenColumns = 0)
{
    /// <summary>The value at a position, or null if the cell is empty.</summary>
    public ExportCell? At(int row, int column)
    {
        foreach (var placed in Cells)
        {
            if (placed.Row == row && placed.Column == column)
            {
                return placed.Cell;
            }
        }

        return null;
    }
}

/// <summary>Writes the sheets as an .xlsx workbook (implemented with ClosedXML in HomeUtilities.Import).</summary>
public interface IExportWorkbookWriter
{
    /// <summary>Writes a workbook with the given sheets.</summary>
    void Write(IReadOnlyList<ExportSheet> sheets, Stream stream);
}
