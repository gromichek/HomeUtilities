using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Consumption;

/// <summary>Consumption of the previous months of a register, the input of <see cref="ConsumptionAnomalies"/>.</summary>
public static class ConsumptionHistory
{
    /// <summary>
    /// Consumption of the <paramref name="months"/> periods ending with <paramref name="upTo"/> (inclusive), oldest first,
    /// as <see cref="ConsumptionCalculator.CalculateForPeriod"/> determines it. Periods whose consumption cannot be
    /// determined (missing reading, negative consumption) are skipped, so the list may be shorter than requested.
    /// </summary>
    /// <param name="chain">The meter chain; a replacement inside the history is handled by the calculator.</param>
    /// <param name="kind">Register kind.</param>
    /// <param name="upTo">Last period of the history, normally the month before the one being checked.</param>
    /// <param name="months">How many periods to look back.</param>
    /// <param name="readingWindowDays">Reading window days of the contract (from, to), or null if there is none.</param>
    public static IReadOnlyList<decimal> Monthly(
        MeterChain chain,
        RegisterKind kind,
        YearMonth upTo,
        int months,
        (int From, int To)? readingWindowDays)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentOutOfRangeException.ThrowIfNegative(months);

        var result = new List<decimal>();
        for (var back = months - 1; back >= 0; back--)
        {
            var consumption = ConsumptionCalculator.CalculateForPeriod(chain, kind, upTo.AddMonths(-back), readingWindowDays);
            if (consumption.IsComplete)
            {
                result.Add(consumption.Quantity);
            }
        }

        return result;
    }
}
