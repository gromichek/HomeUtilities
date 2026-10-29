using ClosedXML.Excel;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import;

/// <summary>Reads .xlsx workbooks with ClosedXML (MIT) for the import wizard: typed cell values plus the text Excel shows.</summary>
public sealed class ClosedXmlWorkbookReader : IWorkbookReader
{
    /// <inheritdoc />
    public IReadOnlyList<SheetInfo> ListSheets(Stream workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        using var book = Open(workbook);
        return
        [
            .. book.Worksheets
                .Where(s => s.Visibility == XLWorksheetVisibility.Visible)
                .Select(s =>
                {
                    var used = s.LastCellUsed();
                    return new SheetInfo(s.Name, used?.Address.RowNumber ?? 0, used?.Address.ColumnNumber ?? 0);
                }),
        ];
    }

    /// <inheritdoc />
    public SheetData ReadSheet(Stream workbook, string sheetName)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrEmpty(sheetName);

        using var book = Open(workbook);
        if (!book.TryGetWorksheet(sheetName, out var sheet))
        {
            throw new InvalidDataException($"The workbook has no sheet '{sheetName}'.");
        }

        var used = sheet.LastCellUsed();
        var rowCount = used?.Address.RowNumber ?? 0;
        var columnCount = used?.Address.ColumnNumber ?? 0;

        // Text of a merged range (a header over several columns) is repeated in every cell of its first row.
        var mergedText = new Dictionary<(int Row, int Column), SheetCell>();
        foreach (var range in sheet.MergedRanges)
        {
            var first = range.FirstCell();
            var cell = Read(first);
            if (cell.Value is not string)
            {
                continue;
            }

            foreach (var column in Enumerable.Range(range.FirstColumn().ColumnNumber(), range.ColumnCount()))
            {
                mergedText[(first.Address.RowNumber, column)] = cell;
            }
        }

        var rows = new List<SheetRow>(rowCount);
        for (var r = 1; r <= rowCount; r++)
        {
            var cells = new SheetCell[columnCount];
            for (var c = 1; c <= columnCount; c++)
            {
                cells[c - 1] = mergedText.TryGetValue((r, c), out var merged) ? merged : Read(sheet.Cell(r, c));
            }

            rows.Add(new SheetRow(r, cells));
        }

        return new SheetData(sheet.Name, rows, columnCount);
    }

    private static XLWorkbook Open(Stream stream)
    {
        try
        {
            return new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("The file is not a readable .xlsx workbook.", ex);
        }
    }

    private static SheetCell Read(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return SheetCell.Empty;
        }

        var text = FormattedText(cell);
        object? value;
        try
        {
            value = cell.DataType switch
            {
                XLDataType.Number => cell.GetDouble() is var d && !double.IsNaN(d) && !double.IsInfinity(d) ? (decimal)d : null,
                XLDataType.DateTime => DateOnly.FromDateTime(cell.GetDateTime()),
                XLDataType.Boolean => cell.GetBoolean() ? "TRUE" : "FALSE",
                XLDataType.Text => cell.GetString(),
                _ => text,
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            // An error cell (#N/A) or a formula without a cached value: treat it as text.
            value = text;
        }

        return value is null ? SheetCell.Empty : new SheetCell(value, text);
    }

    private static string FormattedText(IXLCell cell)
    {
        try
        {
            return cell.GetFormattedString();
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException)
        {
            return cell.Value.ToString();
        }
    }
}
