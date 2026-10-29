using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>One reading of a position in the order of the sheet.</summary>
/// <param name="Date">Reading date.</param>
/// <param name="Value">Dial value.</param>
/// <param name="RowNumber">Sheet row (0 for a reading that is already stored).</param>
public sealed record ReadingPoint(DateOnly Date, decimal Value, int RowNumber);

/// <summary>How a replacement shows in the readings.</summary>
public enum ReplacementSign
{
    /// <summary>The value is lower than the previous one (the new meter starts near 0).</summary>
    Drop = 0,

    /// <summary>The value is much higher than the previous one (the new meter starts high).</summary>
    Jump = 1,
}

/// <summary>A replacement found between two readings.</summary>
/// <param name="Sign">Drop or jump.</param>
/// <param name="InstallationDayRow">True when the reading is an extra row in the same month as the previous one: the readings of the new meter on its installation day.</param>
public sealed record DetectedReplacement(ReplacementSign Sign, bool InstallationDayRow);

/// <summary>
/// Finds meter replacements in a series of readings: a value that is lower than the previous one (and not a plausible
/// roll-over of the dial) or higher than the previous one by more than five typical monthly steps (at least 50).
/// </summary>
public static class ReplacementDetector
{
    /// <summary>Smallest step that can count as a jump.</summary>
    public const decimal MinimumJump = 50m;

    /// <summary>How many typical steps a jump must exceed.</summary>
    public const int JumpFactor = 5;

    private const int MedianWindow = 12;

    /// <summary>The largest step between two readings that is not suspicious: max(5 × median step of the last 12 readings, 50).</summary>
    /// <param name="history">The readings before the new one, oldest first.</param>
    public static decimal MaxPlausibleStep(IReadOnlyList<ReadingPoint> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var tail = history.Count > MedianWindow + 1 ? history.Skip(history.Count - MedianWindow - 1).ToList() : [.. history];
        var steps = new List<decimal>();
        for (var i = 1; i < tail.Count; i++)
        {
            var step = tail[i].Value - tail[i - 1].Value;
            if (step >= 0)
            {
                steps.Add(step);
            }
        }

        return Math.Max(JumpFactor * Median(steps), MinimumJump);
    }

    /// <summary>Checks the reading <paramref name="current"/> against the readings before it.</summary>
    /// <param name="history">The readings before, oldest first (stored ones included).</param>
    /// <param name="current">The new reading.</param>
    /// <param name="digits">Dial digits of the meter, null if unknown.</param>
    /// <returns>The replacement, or null if the reading is an ordinary one.</returns>
    public static DetectedReplacement? Detect(IReadOnlyList<ReadingPoint> history, ReadingPoint current, int? digits)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(current);

        if (history.Count == 0)
        {
            return null;
        }

        var previous = history[^1];
        var step = MaxPlausibleStep(history);
        var sign = current.Value < previous.Value
            ? ReplacementSign.Drop
            : current.Value > previous.Value + step ? ReplacementSign.Jump : (ReplacementSign?)null;
        if (sign is null)
        {
            return null;
        }

        if (sign == ReplacementSign.Drop && IsPlausibleRollover(previous.Value, current.Value, digits, step))
        {
            return null;
        }

        var sameMonth = current.Date.Year == previous.Date.Year && current.Date.Month == previous.Date.Month && current.RowNumber != previous.RowNumber;
        return new DetectedReplacement(sign.Value, sameMonth);
    }

    private static bool IsPlausibleRollover(decimal previous, decimal current, int? digits, decimal maxStep)
    {
        if (digits is not (>= 1 and <= 12))
        {
            return false;
        }

        var consumption = current + (decimal)Math.Pow(10, digits.Value) - previous;
        return consumption >= 0 && consumption <= maxStep;
    }

    private static decimal Median(List<decimal> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }
}
