using ClosedXML.Excel;
using HomeUtilities.Core.Export;

namespace HomeUtilities.Import;

/// <summary>Writes the export sheets as an .xlsx workbook with ClosedXML (MIT): numbers as numbers, dates as dates, bold frozen header.</summary>
public sealed class ClosedXmlExportWriter : IExportWorkbookWriter
{
    private const string DateFormat = "dd.mm.yyyy";
    private const string MonthFormat = "yyyy-mm";

    /// <inheritdoc />
    public void Write(IReadOnlyList<ExportSheet> sheets, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(stream);

        using var workbook = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            var worksheet = workbook.Worksheets.Add(sheet.Name);
            foreach (var placed in sheet.Cells)
            {
                Write(worksheet.Cell(placed.Row, placed.Column), placed.Cell);
            }

            if (sheet.Cells.Count == 0)
            {
                continue;
            }

            var lastColumn = sheet.Cells.Max(c => c.Column);
            var lastRow = sheet.Cells.Max(c => c.Row);
            if (sheet.HeaderRow > 0)
            {
                worksheet.Range(sheet.HeaderRow, 1, sheet.HeaderRow, lastColumn).Style.Fill.BackgroundColor = XLColor.LightGray;
                worksheet.SheetView.FreezeRows(sheet.HeaderRow);
            }

            if (sheet.FrozenColumns > 0)
            {
                worksheet.SheetView.FreezeColumns(sheet.FrozenColumns);
            }

            worksheet.Columns(1, lastColumn).AdjustToContents(Math.Max(1, sheet.HeaderRow), lastRow, 8, 60);
        }

        if (workbook.Worksheets.Count == 0)
        {
            workbook.Worksheets.Add("Sheet");
        }

        workbook.SaveAs(stream);
    }

    private static void Write(IXLCell cell, ExportCell value)
    {
        switch (value.Kind)
        {
            case ExportCellKind.Text:
                cell.Value = value.Text ?? string.Empty;
                break;
            case ExportCellKind.Number:
                cell.Value = value.Number!.Value;
                cell.Style.NumberFormat.Format = value.Decimals <= 0 ? "0" : "0." + new string('0', value.Decimals);
                break;
            case ExportCellKind.Date:
                cell.Value = value.Date!.Value.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = DateFormat;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                break;
            case ExportCellKind.Month:
                cell.Value = value.Date!.Value.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = MonthFormat;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                break;
            default:
                break;
        }

        if (value.IsBold)
        {
            cell.Style.Font.Bold = true;
        }
    }
}
