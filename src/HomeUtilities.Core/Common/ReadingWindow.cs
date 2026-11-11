namespace HomeUtilities.Core.Common;

/// <summary>Calculation of the dates of a meter reading submission window.</summary>
public static class ReadingWindow
{
    /// <summary>
    /// Dates of the window for the given period. Days beyond the month length are clamped;
    /// a window with <paramref name="from"/> &gt; <paramref name="to"/> ends in the next month.
    /// </summary>
    public static (DateOnly From, DateOnly To) For(YearMonth period, int from, int to)
    {
        var toPeriod = from <= to ? period : period.AddMonths(1);
        return (Clamp(period, from), Clamp(toPeriod, to));
    }

    /// <summary>True if <paramref name="day"/> (1–31) is a day of the window <paramref name="from"/>–<paramref name="to"/>.</summary>
    public static bool Contains(int from, int to, int day) =>
        from <= to ? from <= day && day <= to : day >= from || day <= to;

    /// <summary>The days of the window in window order: 28–3 gives 28, 29, 30, 31, 1, 2, 3.</summary>
    public static IReadOnlyList<int> Days(int from, int to)
    {
        if (from < 1 || from > 31 || to < 1 || to > 31)
        {
            return [];
        }

        var days = new List<int>();
        for (var day = from; ; day = day % 31 + 1)
        {
            days.Add(day);
            if (day == to)
            {
                return days;
            }
        }
    }

    /// <summary>
    /// The date a reminder of the window of <paramref name="period"/> is shown. <paramref name="day"/> null, or not a day of the
    /// window, gives the first day. A day at or after the start day is in the start month, an earlier day in the next month;
    /// days beyond the month length fall on its last day. The date is always inside the window.
    /// </summary>
    public static DateOnly ReminderDate(YearMonth period, int from, int to, int? day)
    {
        var (first, last) = For(period, from, to);
        if (day is not { } d || d == from || !Contains(from, to, d))
        {
            return first;
        }

        var date = Clamp(d >= from ? period : period.AddMonths(1), d);
        return date < first ? first : date > last ? last : date;
    }

    private static DateOnly Clamp(YearMonth month, int day) =>
        new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));
}
