using ClosedXML.Excel;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Import;

/// <summary>Writes a report as an .xlsx workbook with ClosedXML (MIT): one sheet, numbers as numbers, months as dates.</summary>
public sealed class ClosedXmlReportWriter : IReportWorkbookWriter
{
    private const string MonthFormat = "yyyy-mm";

    /// <inheritdoc />
    public void Write(ReportSheet sheet, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(stream);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SheetName(sheet.Title));
        var row = 1;

        var title = worksheet.Cell(row++, 1);
        title.Value = sheet.Title;
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 14;

        foreach (var (label, value) in sheet.Filters)
        {
            worksheet.Cell(row, 1).Value = label;
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = value;
            row++;
        }

        row++;
        var headerStart = row;
        foreach (var header in sheet.HeaderRows)
        {
            for (var column = 0; column < header.Count; column++)
            {
                var cell = worksheet.Cell(row, column + 1);
                cell.Value = header[column];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            }

            row++;
        }

        var dataStart = row;
        foreach (var data in sheet.Rows)
        {
            for (var column = 0; column < data.Count; column++)
            {
                WriteCell(worksheet.Cell(row, column + 1), data[column]);
            }

            row++;
        }

        if (sheet.Legend is { Length: > 0 } legend)
        {
            row++;
            var cell = worksheet.Cell(row, 1);
            cell.Value = legend;
            cell.Style.Font.Italic = true;
        }

        var width = Math.Max(sheet.HeaderRows.FirstOrDefault()?.Count ?? 0, sheet.Rows.FirstOrDefault()?.Count ?? 1);
        if (sheet.HeaderRows.Count > 1)
        {
            MergeGroups(worksheet, headerStart, width);
        }

        if (sheet.HeaderRows.Count > 0)
        {
            worksheet.SheetView.FreezeRows(dataStart - 1);
            worksheet.SheetView.FreezeColumns(1);
        }

        worksheet.Columns(1, width).AdjustToContents(headerStart, Math.Max(headerStart, dataStart + sheet.Rows.Count - 1), 8, 40);
        workbook.SaveAs(stream);
    }

    private static void WriteCell(IXLCell cell, SheetCell value)
    {
        switch (value.Kind)
        {
            case SheetCellKind.Text:
                cell.Value = value.Text;
                break;
            case SheetCellKind.Number:
                cell.Value = value.Number!.Value;
                var format = value.Decimals <= 0 ? "#,##0" : "#,##0." + new string('0', value.Decimals);
                cell.Style.NumberFormat.Format = value.IsApproximate ? "\"≈ \"" + format : format;
                break;
            case SheetCellKind.Month:
                cell.Value = value.Month!.Value.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = MonthFormat;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                break;
            default:
                break;
        }

        if (value.Kind == SheetCellKind.Empty && value.IsApproximate)
        {
            cell.Value = "≈";
        }

        if (value.IsBold)
        {
            cell.Style.Font.Bold = true;
        }

        if (value.IsApproximate)
        {
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontColor = XLColor.Gray;
        }
    }

    /// <summary>Merges neighbouring cells of the first header row that carry the same text (the columns of one property).</summary>
    private static void MergeGroups(IXLWorksheet worksheet, int headerRow, int width)
    {
        var start = 1;
        while (start <= width)
        {
            var text = worksheet.Cell(headerRow, start).GetString();
            var end = start;
            while (end < width && text.Length > 0 && worksheet.Cell(headerRow, end + 1).GetString() == text)
            {
                end++;
            }

            if (end > start)
            {
                for (var column = start + 1; column <= end; column++)
                {
                    worksheet.Cell(headerRow, column).Clear(XLClearOptions.Contents);
                }

                worksheet.Range(headerRow, start, headerRow, end).Merge();
                worksheet.Range(headerRow, start, headerRow, end).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            start = end + 1;
        }
    }

    /// <summary>Excel sheet names are at most 31 characters and may not contain \ / ? * [ ] :.</summary>
    private static string SheetName(string title)
    {
        var cleaned = new string([.. title.Where(c => !"\\/?*[]:".Contains(c, StringComparison.Ordinal))]).Trim();
        return cleaned.Length == 0 ? "Report" : cleaned[..Math.Min(31, cleaned.Length)];
    }
}
