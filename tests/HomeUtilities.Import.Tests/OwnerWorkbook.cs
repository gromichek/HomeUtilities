using ClosedXML.Excel;

namespace HomeUtilities.Import.Tests;

/// <summary>
/// An invented workbook with the shapes of the owner's real one (T-027): reading dates on the 1st and then on the 26th, meter
/// replacements of all four patterns, a real-total column that starts later, a heating sheet with dates on the 1st of the next
/// month and no readings, and fixed-amount sheets dated the 2nd. Every number and text is made up.
/// </summary>
public static class OwnerWorkbook
{
    public const string Water = "Water";
    public const string Heat = "Heat";
    public const string Internet = "Internet";

    public static MemoryStream Build()
    {
        using var workbook = new XLWorkbook();
        BuildWater(workbook.Worksheets.Add(Water));
        BuildHeat(workbook.Worksheets.Add(Heat));
        BuildInternet(workbook.Worksheets.Add(Internet));
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // A Date · B Cold · C Hot · D Consumption (both meters) · E Calculated total · F Real total · G Paid · H Note
    private static void BuildWater(IXLWorksheet sheet)
    {
        Header(sheet, "Date", "Cold m3", "Hot m3", "Consumption", "Calculated total", "Real total", "Paid", "Note");

        // Dates on the 1st: the reading for the previous month.
        Row(sheet, 2, D(2004, 2, 1), 100.0, 50.0, 6.0, 10.00, string.Empty, "***", string.Empty);
        Row(sheet, 3, D(2004, 3, 1), 104.0, 52.0, 6.0, 10.00, string.Empty, "***", string.Empty);
        Row(sheet, 4, D(2004, 4, 1), 108.0, 54.0, 6.0, 10.00, string.Empty, "***", string.Empty);

        // Pattern 1: an extra row in the same month with the readings of the new meters on the installation day.
        Row(sheet, 5, D(2004, 5, 1), 112.0, 56.0, 6.0, 10.00, string.Empty, "***", string.Empty);
        Row(sheet, 6, D(2004, 5, 7), 3.0, 477.0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        Row(sheet, 7, D(2004, 6, 1), 7.0, 481.0, 8.0, 10.00, string.Empty, "***", string.Empty);

        // From here on the readings are taken on the 26th. Two dates in one month without a drop are not a replacement.
        Row(sheet, 8, D(2004, 6, 26), 10.0, 483.0, 5.0, 10.00, string.Empty, "***", string.Empty);
        Row(sheet, 9, D(2004, 7, 26), 14.0, 486.0, 7.0, 10.00, string.Empty, "***", string.Empty);

        // Pattern 2: the cold value drops in a regular row, no note; consumption 12 = hot 3 + new cold 6 + old cold 3.
        Row(sheet, 10, D(2004, 8, 26), 6.0, 489.0, 12.0, 10.00, string.Empty, "***", string.Empty);

        // Pattern 3: the hot value drops, the note names the date; consumption 10 = cold 3 + new hot 3 + old hot 4.
        Row(sheet, 11, D(2004, 9, 26), 9.0, 3.0, 10.0, 10.00, string.Empty, "***", "поменяли счетчики 30.08.2004");
        Row(sheet, 12, D(2004, 10, 26), 12.0, 7.0, 7.0, 10.00, string.Empty, "***", string.Empty);

        // Pattern 4: the note gives the exact final and initial values (cold: old final 15.3, new initial 0.072).
        Row(sheet, 13, D(2004, 11, 26), 0.5, 10.0, string.Empty, 10.00, string.Empty, "***", "skaititaja nr.: Auksta - 1210007 rad:15,300 rad:0,072");

        // From 2005 the supplier's real total is in column F, the calculated total stays in column E.
        Row(sheet, 14, D(2004, 12, 26), 3.5, 13.0, string.Empty, 10.00, string.Empty, "***", string.Empty);
        Row(sheet, 15, D(2005, 1, 26), 6.5, 16.0, string.Empty, 10.00, 11.20, "***", string.Empty);
        Row(sheet, 16, D(2005, 2, 26), 9.5, 19.0, string.Empty, 10.00, 11.30, "***", string.Empty);
    }

    // A Date (1st of the month after the service month) · B Hot water m3 on the bill · C Total · D Paid
    private static void BuildHeat(IXLWorksheet sheet)
    {
        Header(sheet, "Date", "Hot water m3", "Total", "Paid");
        Row(sheet, 2, D(2004, 6, 1), 3.0, 40.00, "***");
        Row(sheet, 3, D(2004, 7, 1), 2.0, 38.50, "***");
        Row(sheet, 4, D(2004, 8, 1), 4.0, 41.00, string.Empty);
    }

    // A Date (the 2nd: the payment date for the previous month) · B Amount · C Paid
    private static void BuildInternet(IXLWorksheet sheet)
    {
        Header(sheet, "Month", "Amount", "Paid");
        Row(sheet, 2, D(2022, 12, 2), 15.00, "***");
        Row(sheet, 3, D(2023, 1, 2), 15.00, "***");

        // The service became prepaid: the first prepaid row pays two months and a connection fee.
        Row(sheet, 4, D(2023, 2, 2), 45.00, "***");
        Row(sheet, 5, D(2023, 4, 2), 15.00, "***");
    }

    private static DateTime D(int year, int month, int day) => new(year, month, day);

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
                case double number:
                    cell.Value = number;
                    break;
                default:
                    throw new ArgumentException($"Unsupported value {values[i]}");
            }
        }
    }
}
