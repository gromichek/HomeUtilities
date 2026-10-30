using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Export;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Import.Tests;

public class WorkbookExportTests
{
    private static string Text(ExportSheet sheet, int row, int column) => sheet.At(row, column)?.Text ?? string.Empty;

    private static decimal? Num(ExportSheet sheet, int row, int column) => sheet.At(row, column)?.Number;

    private static DateOnly? Day(ExportSheet sheet, int row, int column) => sheet.At(row, column)?.Date;

    [Fact]
    public void A_contract_with_a_profile_is_laid_out_like_the_profile()
    {
        var sheets = new ExportScenario().Export();

        Assert.Equal(["Total", "Aqua Water"], sheets.Select(s => s.Name));
        var water = sheets[1];
        Assert.Equal(["Month", "Cold m3", "Hot m3", "Total", "Paid", "Comment"], Enumerable.Range(1, 6).Select(c => Text(water, 1, c)));
        Assert.Equal(1, water.HeaderRow);

        // Row 2: December 2013 (PreviousMonth rule: dated the 1st of January), 12.34 LVL as stored, paid, note without its header.
        Assert.Equal(new DateOnly(2014, 1, 1), Day(water, 2, 1));
        Assert.Equal((100m, 50m, 12.34m), (Num(water, 2, 2), Num(water, 2, 3), Num(water, 2, 4)));
        Assert.Equal(("***", "tariff changed"), (Text(water, 2, 5), Text(water, 2, 6)));

        // Row 3: January 2014, paid on 2014-02-12 (no paid-date column in the profile).
        Assert.Equal((new DateOnly(2014, 2, 1), 105m, 53m, 13.00m, "***"), (Day(water, 3, 1), Num(water, 3, 2), Num(water, 3, 3), Num(water, 3, 4), Text(water, 3, 5)));

        // Row 5: March 2014: the new cold meter's reading, not paid.
        Assert.Equal((new DateOnly(2014, 4, 1), 6m, 59m, 15.00m, string.Empty), (Day(water, 6, 1), Num(water, 6, 2), Num(water, 6, 3), Num(water, 6, 4), Text(water, 6, 5)));
    }

    [Fact]
    public void A_meter_replacement_gets_an_extra_row_with_the_new_initial_reading_and_a_note()
    {
        var water = new ExportScenario().Export()[1];

        // Rows: Dec, Jan, Feb, replacement (2014-03-15), Mar, Apr.
        Assert.Equal(7, water.Cells.Max(c => c.Row));
        Assert.Equal(new DateOnly(2014, 3, 1), Day(water, 4, 1));
        Assert.Equal(110m, Num(water, 4, 2));

        Assert.Equal(new DateOnly(2014, 3, 15), Day(water, 5, 1));
        Assert.Equal(0m, Num(water, 5, 2));
        Assert.Null(water.At(5, 3));
        Assert.Null(water.At(5, 4));
        Assert.Equal("replaced 15.03.2014: cold - CW-20140 reading 110 reading 0", Text(water, 5, 6));
    }

    [Fact]
    public void Without_a_profile_the_standard_layout_is_used()
    {
        // A water contract with a reading window 20-25: the readings of a month are taken on the 22nd of that month.
        var water = new ExportScenario(ImportPeriodRule.ReadingWindow).Export(withProfile: false)[1];

        Assert.Equal(
            ["Date", "ColdWater", "HotWater", "Consumption", "Net", "VAT %", "VAT", "Calculated", "Bill total", "Difference", "Paid", "Paid on", "Notes", "Currency"],
            Enumerable.Range(1, 14).Select(c => Text(water, 1, c)));
        Assert.Equal(1, water.FrozenColumns);

        // December 2013: the reading date, both readings, the bill with net, VAT %, VAT, paid on 2014-01-10, in LVL as stored.
        Assert.Equal(new DateOnly(2013, 12, 22), Day(water, 2, 1));
        Assert.Equal((100m, 50m), (Num(water, 2, 2), Num(water, 2, 3)));
        Assert.Equal((10.20m, 21m, 2.14m, 12.34m), (Num(water, 2, 5), Num(water, 2, 6), Num(water, 2, 7), Num(water, 2, 9)));
        Assert.Equal(("***", new DateOnly(2014, 1, 10), "Comment: tariff changed", "LVL"), (Text(water, 2, 11), Day(water, 2, 12), Text(water, 2, 13), Text(water, 2, 14)));

        // January 2014: consumption 5 m3 cold + 3 m3 hot = 8 m3 from the readings of December and January.
        Assert.Equal((105m, 53m, 8m), (Num(water, 3, 2), Num(water, 3, 3), Num(water, 3, 4)));
        Assert.Equal("EUR", Text(water, 3, 14));
    }

    [Fact]
    public void Amounts_before_2014_are_exported_as_stored_or_converted_to_EUR()
    {
        var scenario = new ExportScenario();

        var stored = scenario.Export()[1];
        var converted = scenario.Export(new WorkbookExportOptions(ConvertToEur: true))[1];

        // 12.34 LVL / 0.702804 = 17.5582… EUR.
        Assert.Equal(12.34m, Num(stored, 2, 4));
        Assert.Equal(17.56m, Num(converted, 2, 4));
        Assert.Equal(13.00m, Num(converted, 3, 4));
    }

    [Fact]
    public void The_period_limits_the_rows()
    {
        var water = new ExportScenario().Export(new WorkbookExportOptions(From: new YearMonth(2014, 2), To: new YearMonth(2014, 3)))[1];

        // Feb, replacement (belongs to Feb under the PreviousMonth rule), Mar.
        Assert.Equal(new DateOnly(2014, 3, 1), Day(water, 2, 1));
        Assert.Equal(new DateOnly(2014, 3, 15), Day(water, 3, 1));
        Assert.Equal(new DateOnly(2014, 4, 1), Day(water, 4, 1));
        Assert.Equal(4, water.Cells.Max(c => c.Row));
    }

    [Fact]
    public void Contracts_that_are_not_chosen_get_no_sheet()
    {
        var sheets = new ExportScenario().Export(new WorkbookExportOptions(ContractIds: [Guid.NewGuid()]));

        Assert.Empty(sheets);
    }

    [Fact]
    public void The_total_sheet_has_the_numbers_of_the_costs_report_by_contract()
    {
        var scenario = new ExportScenario();
        var total = scenario.Export()[0];

        Assert.Equal(["Month (EUR)", "Water", "Sum"], Enumerable.Range(1, 3).Select(c => Text(total, 1, c)));

        // December 2013: 12.34 LVL = 17.56 EUR; January 2014: 13.00 EUR.
        Assert.Equal(new DateOnly(2013, 12, 1), Day(total, 2, 1));
        Assert.Equal((17.56m, 17.56m), (Num(total, 2, 2), Num(total, 2, 3)));
        Assert.Equal(13.00m, Num(total, 3, 2));

        var report = ReportBuilder.Costs(scenario.Data(), new ReportFilter(new YearMonth(2013, 12), new YearMonth(2014, 4), Grouping: CostGrouping.Contract));
        var expected = report.Rows.Where(r => r.Kind == ReportRowKind.Month).Select(r => r.Cells[0].Value).ToList();
        Assert.Equal(expected, Enumerable.Range(2, 5).Select(r => Num(total, r, 2)));

        // Totals: 17.56 + 13.00 + 11.50 + 15.00 + 9.99 = 67.05.
        Assert.Equal(("Sum", 67.05m, 67.05m), (Text(total, 7, 1), Num(total, 7, 2), Num(total, 7, 3)));
    }

    [Theory]
    [InlineData("Supplier: a/b\\c?d*e[f]g", "Supplier abcdefg")]
    [InlineData("'quoted'", "quoted")]
    [InlineData("", "Sheet")]
    [InlineData("A very long supplier name with a contract title", "A very long supplier name with ")]
    public void Sheet_names_are_valid_for_Excel(string wanted, string expected)
    {
        var name = WorkbookExporter.Sanitize(wanted);

        Assert.Equal(expected.TrimEnd(), name);
        Assert.True(name.Length <= 31);
    }

    [Fact]
    public void Contracts_with_the_same_name_get_unique_sheet_names()
    {
        var scenario = new ExportScenario();
        var second = new Contract { PropertyId = scenario.Property.Id, SupplierId = scenario.Supplier.Id, CategoryId = scenario.Contract.CategoryId, Title = "Water", ValidFrom = new DateOnly(2013, 1, 1) };
        scenario.Bills.Add(new HomeUtilities.Core.Billing.Bill { PropertyId = scenario.Property.Id, CategoryId = scenario.Contract.CategoryId, ContractId = second.Id, Period = new YearMonth(2014, 1), Gross = 5m });
        var data = new ReportData([scenario.Property], [], [scenario.Contract, second], [], [scenario.Cold, scenario.ColdNew, scenario.Hot], scenario.Bills, [], []);

        var sheets = WorkbookExporter.Build(new WorkbookExportInput(data, scenario.Payments, [scenario.Supplier], []), new WorkbookExportOptions(), ExportScenario.Labels);

        Assert.Equal(["Total", "Aqua Water", "Aqua Water (2)"], sheets.Select(s => s.Name));
    }

    [Fact]
    public void The_writer_stores_numbers_as_numbers_and_dates_as_dates()
    {
        var sheets = new ExportScenario().Export();
        using var stream = new MemoryStream();

        new ClosedXmlExportWriter().Write(sheets, stream);

        stream.Position = 0;
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var water = workbook.Worksheet("Aqua Water");
        Assert.Equal(["Total", "Aqua Water"], workbook.Worksheets.Select(s => s.Name));
        Assert.True(water.Cell(1, 1).Style.Font.Bold);
        Assert.Equal(1, water.SheetView.SplitRow);
        Assert.Equal(new DateTime(2014, 1, 1), water.Cell(2, 1).GetDateTime());
        Assert.Equal(100d, water.Cell(2, 2).GetDouble());
        Assert.Equal("0.000", water.Cell(2, 2).Style.NumberFormat.Format);
        Assert.Equal(12.34d, water.Cell(2, 4).GetDouble());
        Assert.Equal("***", water.Cell(2, 5).GetString());
    }
}
