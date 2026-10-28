using System.Globalization;
using System.Text;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Tests.Reports;

public class ReportCsvWriterTests
{
    private static ReportSheet Sheet() => new(
        "Costs; \"all\"",
        [("Period", "2026-01 – 2026-02")],
        [["", "Flat"], ["Period", "Water"]],
        [
            [SheetCell.FromMonth(new DateOnly(2026, 1, 1)), SheetCell.FromNumber(1234.5m, 2)],
            [SheetCell.FromMonth(new DateOnly(2026, 2, 1)), SheetCell.FromNumber(7.125m, 3, approximate: true)],
            [SheetCell.FromText("Total", bold: true), new SheetCell(SheetCellKind.Empty)],
        ],
        "≈ incomplete");

    private static (byte[] Bytes, string[] Lines) Write(CultureInfo culture)
    {
        using var stream = new MemoryStream();
        ReportCsvWriter.Write(Sheet(), stream, culture);
        var bytes = stream.ToArray();
        return (bytes, Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n"));
    }

    [Fact]
    public void The_file_is_utf8_with_a_byte_order_mark_and_uses_semicolons()
    {
        var (bytes, lines) = Write(new CultureInfo("lv-LV"));

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("\"Costs; \"\"all\"\"\"", lines[0]);
        Assert.Equal("Period;2026-01 – 2026-02", lines[1]);
        Assert.Equal(";Flat", lines[3]);
        Assert.Equal("Period;Water", lines[4]);
    }

    [Fact]
    public void Numbers_follow_the_culture_without_thousands_separators_and_approximate_ones_are_marked()
    {
        var (_, latvian) = Write(new CultureInfo("lv-LV"));
        var (_, invariant) = Write(CultureInfo.InvariantCulture);

        Assert.Equal("2026-01;1234,50", latvian[5]);
        Assert.Equal("2026-02;≈ 7,125", latvian[6]);
        Assert.Equal("Total;", latvian[7]);
        Assert.Equal("2026-01;1234.50", invariant[5]);
        Assert.Equal("≈ incomplete", latvian[9]);
    }
}
