using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>A reading window the contract should get so that its calculations assign the imported readings to the right months.</summary>
/// <param name="From">First day of the window (in the month of the service).</param>
/// <param name="To">Last day of the window (in the next month).</param>
/// <param name="Rows">Number of sheet rows that need it.</param>
public sealed record ReadingWindowSuggestion(int From, int To, int Rows);

/// <summary>
/// A sheet whose reading dates are early in a month and whose period rule gives the month before (readings taken on the 1st
/// belong to the previous month) only calculates correctly when the contract has a reading window that contains those dates;
/// without it the charges look for the readings in the month itself and every amount shifts by one row.
/// </summary>
public static class ImportReadingWindowAdvice
{
    /// <summary>Reading dates after this day of the month are not taken for the end of the suggested window.</summary>
    public const int MaxEndDay = 10;

    /// <summary>The window starts on this day of the service month.</summary>
    public const int StartDay = 28;

    /// <summary>The window ends this many days after the last early reading day seen.</summary>
    public const int EndMargin = 2;

    /// <summary>
    /// The window to suggest, or null when no row needs one. A row needs it when it creates readings, its period is before the
    /// month of its date (the rule moved it back), the owner did not correct it, and the contract's window for that period
    /// is missing or does not contain the date. The window is 28 – (latest day seen + 2, at most 10).
    /// </summary>
    public static ReadingWindowSuggestion? Suggest(IEnumerable<PlannedRow> rows, Contract contract)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(contract);

        var days = new List<int>();
        foreach (var row in rows)
        {
            if (row.HasErrors || row.Readings.Count == 0 || row.Override is not null
                || row.RowDate is not { } date || row.Period is not { } period
                || period >= YearMonth.From(date) || date.Day > MaxEndDay)
            {
                continue;
            }

            if (contract.GetReadingWindow(period) is { } window && window.From <= date && date <= window.To)
            {
                continue;
            }

            days.Add(date.Day);
        }

        return days.Count == 0 ? null : new ReadingWindowSuggestion(StartDay, Math.Min(MaxEndDay, days.Max() + EndMargin), days.Count);
    }
}
