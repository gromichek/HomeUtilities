using System.Globalization;
using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Month and date-range texts in the app language (UI_GUIDELINES §9). The long form comes from the culture's month names; the
/// short 3-letter form comes from the app's own resource keys <c>MonthShort_1…12</c>, because .NET's abbreviated Russian names
/// have different lengths ("февр.", "март", "июнь", "сент.").
/// </summary>
public static class MonthFormat
{
    /// <summary>"Сентябрь 2026" / "September 2026": the long month, nominative, first letter upper case.</summary>
    public static string Long(DateOnly date) => Capitalize(date.ToString("MMMM yyyy", CultureInfo.CurrentCulture));

    /// <summary>The long month of a year-month.</summary>
    public static string Long(YearMonth month) => Long(month.FirstDay);

    /// <summary>"сен" / "Sep": the app's 3-letter name of month 1…12.</summary>
    public static string ShortName(ILocalizer localizer, int month)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return localizer["MonthShort_" + month.ToString(CultureInfo.InvariantCulture)];
    }

    /// <summary>"сен 26" / "Sep 26": the short month and the two-digit year (chart axis).</summary>
    public static string ShortWithYear(ILocalizer localizer, int year, int month) =>
        $"{ShortName(localizer, month)} {(year % 100).ToString("00", CultureInfo.InvariantCulture)}";

    /// <summary>"28 окт" / "28 Oct": a day and the short month.</summary>
    public static string DayAndMonth(ILocalizer localizer, DateOnly date) =>
        $"{date.Day.ToString(CultureInfo.InvariantCulture)} {ShortName(localizer, date.Month)}";

    /// <summary>"25–28 окт" within one month, "28 окт – 3 ноя" across months.</summary>
    public static string Range(ILocalizer localizer, DateOnly from, DateOnly to) =>
        from.Year == to.Year && from.Month == to.Month
            ? $"{from.Day.ToString(CultureInfo.InvariantCulture)}–{DayAndMonth(localizer, to)}"
            : $"{DayAndMonth(localizer, from)} – {DayAndMonth(localizer, to)}";

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
}
