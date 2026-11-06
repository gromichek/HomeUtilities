using System.Globalization;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>One format of a full date everywhere (UI_GUIDELINES §9, C-10).</summary>
public class DateFormatTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    [InlineData("lv-LV")]
    [InlineData("")]
    public void A_full_date_is_dd_MM_yyyy_in_every_culture(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            // 15 March 2023: the English system pattern would print 3/15/2023.
            Assert.Equal("15.03.2023", DateFormat.Full(new DateOnly(2023, 3, 15)));
            Assert.Equal("05.01.2026", DateFormat.Full(new DateOnly(2026, 1, 5)));
            Assert.Equal("15.03.2023", DateFormat.Full(new DateTime(2023, 3, 15, 23, 59, 0)));
            Assert.Equal(DateFormat.Full(new DateOnly(2023, 3, 15)), BillText.Date(new DateOnly(2023, 3, 15)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
