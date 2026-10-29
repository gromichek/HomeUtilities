using ClosedXML.Excel;

namespace HomeUtilities.Import.Tests;

/// <summary>
/// Builds an invented workbook shaped like DATA_MODEL §12 (one sheet per supplier, one row per month). It contains no real data;
/// every number and text is made up for the tests.
/// </summary>
public static class SyntheticWorkbook
{
    public const string Water = "Water";
    public const string Heat = "Heat";
    public const string Waste = "Waste";
    public const string Electricity = "Electricity";
    public const string Propane = "Propane";

    /// <summary>Builds the workbook and returns it as a rewound stream.</summary>
    public static MemoryStream Build()
    {
        using var workbook = new XLWorkbook();
        BuildWater(workbook.Worksheets.Add(Water));
        BuildHeat(workbook.Worksheets.Add(Heat));
        BuildWaste(workbook.Worksheets.Add(Waste));
        BuildElectricity(workbook.Worksheets.Add(Electricity));
        BuildPropane(workbook.Worksheets.Add(Propane));
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // A Month · B Cold m3 · C Hot m3 · D Total · E Paid · F Comment
    private static void BuildWater(IXLWorksheet sheet)
    {
        Header(sheet, "Month", "Cold m3", "Hot m3", "Total", "Paid", "Comment");

        // Row 2: date cell, LVL period, paid.
        Row(sheet, 2, new DateTime(2013, 11, 1), 100.5, 50.25, 12.34, "***", string.Empty);

        // Row 3: month as "MM.yyyy" text, readings with a decimal comma as text, a note.
        Row(sheet, 3, "12.2013", "103,2", 52, 13.00, "***", "tariff changed");

        // Row 4: Russian month name, EUR period, not paid yet.
        Row(sheet, 4, "Январь 2014", 106, 54, 11.50, string.Empty, string.Empty);

        // Row 5: English month name, no total yet: readings only.
        Row(sheet, 5, "Feb 2014", 109, 56, string.Empty, string.Empty, string.Empty);

        // Row 6: Latvian month name; the cold meter was replaced, so the dial is lower than before → error.
        Row(sheet, 6, "marts 2014", 20, 58, 15.00, "***", "cold meter replaced");

        // Row 7 is empty; what follows must not be read when no last row is given.
        Row(sheet, 8, new DateTime(2014, 4, 1), 25, 60, 9.99, string.Empty, string.Empty);
    }

    // A Month · B Hot m3 (the same meter as the water sheet) · C Net · D VAT % · E Total · F Paid · G Note
    private static void BuildHeat(IXLWorksheet sheet)
    {
        Header(sheet, "Period", "Hot m3", "Net", "VAT %", "Total", "Paid", "Note");
        Row(sheet, 2, new DateTime(2014, 2, 1), 56, 100.00, 12, 112.00, "***", "heating");
        Row(sheet, 3, new DateTime(2014, 3, 1), 58, 100.00, 12, 113.50, string.Empty, string.Empty);
        Row(sheet, 4, new DateTime(2014, 4, 1), 60, string.Empty, string.Empty, 120.00, "***", string.Empty);
    }

    // A Month · B Net · C VAT · D Total · E Paid · F Paid on
    private static void BuildWaste(IXLWorksheet sheet)
    {
        Header(sheet, "Month", "Net", "VAT", "Total", "Paid", "Paid on");
        Row(sheet, 2, "2014-01", 10.00, 2.10, 12.10, "***", new DateTime(2014, 2, 10));
        Row(sheet, 3, "2014-02", 10.00, 2.10, 12.10, "***", string.Empty);
        Row(sheet, 4, "2014-03", string.Empty, string.Empty, 12.10, string.Empty, string.Empty);
        Row(sheet, 5, "2014-03", string.Empty, string.Empty, 12.10, string.Empty, string.Empty);
    }

    // A Month · B kWh · C Reading date · D Total
    private static void BuildElectricity(IXLWorksheet sheet)
    {
        Header(sheet, "Month", "kWh", "Reading date", "Total");
        Row(sheet, 2, new DateTime(2014, 1, 1), 1000, new DateTime(2014, 1, 25), 20.00);
        Row(sheet, 3, new DateTime(2014, 2, 1), 1250, new DateTime(2014, 2, 26), 28.75);
        Row(sheet, 4, new DateTime(2014, 3, 1), 1500, new DateTime(2014, 3, 27), "n/a");
    }

    // A Month · B m3 · C Total
    private static void BuildPropane(IXLWorksheet sheet)
    {
        Header(sheet, "Month", "m3", "Total");
        Row(sheet, 2, new DateTime(2014, 1, 1), 10, 30.00);
        Row(sheet, 3, new DateTime(2014, 2, 1), 12, 30.00);
    }

    private static void Header(IXLWorksheet sheet, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = titles[i];
        }
    }

    private static void Row(IXLWorksheet sheet, int row, params object[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            switch (values[i])
            {
                case string { Length: 0 }:
                    break;
                case string text:
                    cell.Value = text;
                    break;
                case DateTime date:
                    cell.Value = date;
                    cell.Style.DateFormat.Format = "dd.MM.yyyy";
                    break;
                case int number:
                    cell.Value = number;
                    break;
                case double number:
                    cell.Value = number;
                    break;
                default:
                    throw new ArgumentException($"Unsupported value {values[i]}");
            }
        }
    }
}
