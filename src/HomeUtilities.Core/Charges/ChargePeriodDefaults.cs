using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Charges;

/// <summary>Chooses the month the charges screen shows when it opens.</summary>
public static class ChargePeriodDefaults
{
    /// <summary>
    /// The current month if the reading window of any contract of the property has already started this month
    /// (today is on or after the window start), otherwise the previous month. Without any reading window it is
    /// the previous month: the readings of the current month are not in yet.
    /// </summary>
    /// <param name="contracts">The contracts of one property.</param>
    /// <param name="today">Today.</param>
    public static YearMonth For(IEnumerable<Contract> contracts, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(contracts);

        var current = YearMonth.From(today);
        var windowStarted = contracts
            .Where(c => c.IsActiveOn(today) && c.GetReadingWindow(current) is not null)
            .Any(c => c.GetReadingWindow(current)!.Value.From <= today);
        return windowStarted ? current : current.AddMonths(-1);
    }
}
