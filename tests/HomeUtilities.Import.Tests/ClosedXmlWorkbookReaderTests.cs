using ClosedXML.Excel;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ClosedXmlWorkbookReaderTests
{
    private readonly ClosedXmlWorkbookReader _reader = new();

    [Fact]
    public void Lists_the_sheets_with_their_sizes()
    {
        using var stream = SyntheticWorkbook.Build();

        var sheets = _reader.ListSheets(stream);

        Assert.Equal(
            [SyntheticWorkbook.Water, SyntheticWorkbook.Heat, SyntheticWorkbook.Waste, SyntheticWorkbook.Electricity, SyntheticWorkbook.Propane],
            sheets.Select(s => s.Name));
        Assert.Equal((8, 6), (sheets[0].RowCount, sheets[0].ColumnCount));
        Assert.Equal((3, 3), (sheets[4].RowCount, sheets[4].ColumnCount));
    }

    [Fact]
    public void Reads_typed_values_and_the_formatted_text()
    {
        using var stream = SyntheticWorkbook.Build();

        var sheet = _reader.ReadSheet(stream, SyntheticWorkbook.Water);

        Assert.Equal(8, sheet.RowCount);
        Assert.Equal("Cold m3", sheet.Row(1)!.Cell(2).Text);
        var first = sheet.Row(2)!;
        Assert.Equal(new DateOnly(2013, 11, 1), first.Cell(1).Value);
        Assert.Equal("01.11.2013", first.Cell(1).Text);
        Assert.Equal(100.5m, first.Cell(2).Value);
        Assert.Equal("***", first.Cell(5).Value);
        Assert.True(first.Cell(6).IsEmpty);
        Assert.Equal("12.2013", sheet.Row(3)!.Cell(1).Value);
        Assert.Equal("103,2", sheet.Row(3)!.Cell(2).Value);
        Assert.True(sheet.Row(7)!.Cells.All(c => c.IsEmpty));
    }

    [Fact]
    public void A_missing_sheet_is_reported()
    {
        using var stream = SyntheticWorkbook.Build();

        Assert.Throws<InvalidDataException>(() => _reader.ReadSheet(stream, "Nope"));
    }

    [Fact]
    public void A_file_that_is_not_a_workbook_is_reported()
    {
        using var stream = new MemoryStream("not a workbook"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => _reader.ListSheets(stream));
    }

    [Fact]
    public void Formulas_give_their_cached_values()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("F");
            sheet.Cell(1, 1).Value = 2;
            sheet.Cell(1, 2).FormulaA1 = "A1*3";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var data = _reader.ReadSheet(stream, "F");

        Assert.Equal(6m, data.Row(1)!.Cell(2).Value);
    }

    [Fact]
    public void A_merged_header_text_is_repeated_over_its_columns()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("M");
            sheet.Cell(1, 2).Value = "Readings";
            sheet.Range(1, 2, 1, 3).Merge();
            sheet.Cell(2, 2).Value = 5;
            sheet.Cell(2, 3).Value = 6;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var data = _reader.ReadSheet(stream, "M");

        Assert.Equal(["", "Readings", "Readings"], data.Row(1)!.Cells.Select(c => c.Text));
        Assert.Equal(6m, data.Row(2)!.Cell(3).Value);
    }

    [Fact]
    public void A_thousand_rows_are_read()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Big");
            for (var r = 1; r <= 1000; r++)
            {
                sheet.Cell(r, 1).Value = new DateTime(2000, 1, 1).AddMonths(r);
                sheet.Cell(r, 2).Value = r * 1.5;
            }

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var data = _reader.ReadSheet(stream, "Big");

        Assert.Equal(1000, data.RowCount);
        Assert.Equal(1500m, data.Row(1000)!.Cell(2).Value);
    }
}
