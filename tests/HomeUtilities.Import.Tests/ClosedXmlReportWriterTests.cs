using ClosedXML.Excel;
using HomeUtilities.Core.Reports;
using HomeUtilities.Import;

namespace HomeUtilities.Import.Tests;

public class ClosedXmlReportWriterTests
{
    private static ReportSheet Sheet() => new(
        "Costs",
        [("Period", "2026-01 – 2026-03"), ("Properties", "Flat, Cottage")],
        [
            ["", "Flat", "Flat", "Cottage"],
            ["Period", "Water", "Total", "Power"],
        ],
        [
            [SheetCell.FromMonth(new DateOnly(2026, 1, 1)), SheetCell.FromNumber(20.5m, 2), SheetCell.FromNumber(20.5m, 2), SheetCell.FromNumber(40m, 2)],
            [SheetCell.FromMonth(new DateOnly(2026, 2, 1)), SheetCell.FromNumber(12.345m, 3, approximate: true), SheetCell.FromNumber(12.345m, 3, approximate: true), new SheetCell(SheetCellKind.Empty, IsApproximate: true)],
            [SheetCell.FromText("Total", bold: true), SheetCell.FromNumber(32.845m, 3, bold: true), SheetCell.FromNumber(32.845m, 3, bold: true), SheetCell.FromNumber(40m, 2, bold: true)],
        ],
        "≈ incomplete data");

    private static XLWorkbook Roundtrip(ReportSheet sheet)
    {
        using var stream = new MemoryStream();
        new ClosedXmlReportWriter().Write(sheet, stream);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    [Fact]
    public void The_workbook_has_one_sheet_with_the_title_filters_headers_and_rows()
    {
        using var workbook = Roundtrip(Sheet());

        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Costs", sheet.Name);
        Assert.Equal("Costs", sheet.Cell(1, 1).GetString());
        Assert.Equal(("Period", "2026-01 – 2026-03"), (sheet.Cell(2, 1).GetString(), sheet.Cell(2, 2).GetString()));
        Assert.Equal(("Properties", "Flat, Cottage"), (sheet.Cell(3, 1).GetString(), sheet.Cell(3, 2).GetString()));

        // Row 4 is empty; the header rows are 5 and 6, the data from row 7.
        Assert.Equal("Flat", sheet.Cell(5, 2).GetString());
        Assert.Equal("Cottage", sheet.Cell(5, 4).GetString());
        Assert.Equal(["Period", "Water", "Total", "Power"], Enumerable.Range(1, 4).Select(c => sheet.Cell(6, c).GetString()));
    }

    [Fact]
    public void Numbers_are_numbers_with_their_decimals_and_months_are_dates()
    {
        using var workbook = Roundtrip(Sheet());
        var sheet = workbook.Worksheet(1);

        var month = sheet.Cell(7, 1);
        Assert.Equal(XLDataType.DateTime, month.DataType);
        Assert.Equal(new DateTime(2026, 1, 1), month.GetDateTime());
        var money = sheet.Cell(7, 2);
        Assert.Equal(XLDataType.Number, money.DataType);
        Assert.Equal(20.5, money.GetDouble());
        Assert.Equal("#,##0.00", money.Style.NumberFormat.Format);
        Assert.Equal(40.0, sheet.Cell(7, 4).GetDouble());
        Assert.Equal(12.345, sheet.Cell(8, 2).GetDouble());
        Assert.EndsWith("#,##0.000", sheet.Cell(8, 2).Style.NumberFormat.Format, StringComparison.Ordinal);
    }

    [Fact]
    public void Approximate_cells_are_marked_and_the_totals_row_is_bold()
    {
        using var workbook = Roundtrip(Sheet());
        var sheet = workbook.Worksheet(1);

        Assert.StartsWith("\"≈ \"", sheet.Cell(8, 2).Style.NumberFormat.Format, StringComparison.Ordinal);
        Assert.True(sheet.Cell(8, 2).Style.Font.Italic);
        Assert.Equal("≈", sheet.Cell(8, 4).GetString());
        Assert.Equal("Total", sheet.Cell(9, 1).GetString());
        Assert.True(sheet.Cell(9, 2).Style.Font.Bold);
        Assert.Equal(32.845, sheet.Cell(9, 2).GetDouble());
        Assert.Equal("≈ incomplete data", sheet.Cell(11, 1).GetString());
    }

    [Fact]
    public void The_property_names_above_their_columns_are_merged()
    {
        using var workbook = Roundtrip(Sheet());
        var sheet = workbook.Worksheet(1);

        Assert.Contains(sheet.MergedRanges, r => r.RangeAddress.ToString() == "B5:C5");
        Assert.Equal("Flat", sheet.Cell(5, 2).GetString());
    }

    [Fact]
    public void A_title_with_characters_excel_does_not_allow_becomes_a_valid_sheet_name()
    {
        var sheet = Sheet() with { Title = "Calculated vs billed: very/long [name] that is longer than thirty-one" };

        using var workbook = Roundtrip(sheet);

        var name = workbook.Worksheet(1).Name;
        Assert.True(name.Length <= 31);
        Assert.DoesNotContain(':', name);
    }
}
