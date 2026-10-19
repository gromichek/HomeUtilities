namespace HomeUtilities.Core.Consumption;

/// <summary>Detection of unusual consumption compared with earlier periods.</summary>
public static class ConsumptionAnomalies
{
    /// <summary>Minimum number of earlier periods needed to judge a high value.</summary>
    public const int MinHistoryForHigh = 3;

    /// <summary>
    /// <see cref="ConsumptionWarningCodes.UnusuallyHigh"/> when there are at least 3 history values and
    /// <paramref name="current"/> exceeds <paramref name="factor"/> times their average;
    /// <see cref="ConsumptionWarningCodes.ZeroConsumption"/> when <paramref name="current"/> is 0 and the average is positive.
    /// </summary>
    /// <param name="current">Consumption of the period being checked.</param>
    /// <param name="history">Consumption of earlier periods (e.g. the last 12).</param>
    /// <param name="factor">Multiplier of the average above which consumption is unusually high.</param>
    public static IReadOnlyList<ConsumptionWarning> Check(decimal current, IReadOnlyList<decimal> history, decimal factor = 3m)
    {
        ArgumentNullException.ThrowIfNull(history);

        var warnings = new List<ConsumptionWarning>();
        if (history.Count == 0)
        {
            return warnings;
        }

        var average = history.Average();
        if (history.Count >= MinHistoryForHigh && current > factor * average)
        {
            warnings.Add(new(ConsumptionWarningCodes.UnusuallyHigh, [current, average]));
        }

        if (current == 0 && average > 0)
        {
            warnings.Add(new(ConsumptionWarningCodes.ZeroConsumption, [average]));
        }

        return warnings;
    }
}
