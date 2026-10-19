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

    private static DateOnly Clamp(YearMonth month, int day) =>
        new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));
}
