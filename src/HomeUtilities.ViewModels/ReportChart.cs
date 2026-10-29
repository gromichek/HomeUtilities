using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels;

/// <summary>How the series of a chart are drawn.</summary>
public enum ChartKind
{
    /// <summary>Bars side by side per period.</summary>
    Bars = 0,

    /// <summary>Bars on top of each other per period (the sum is the total).</summary>
    StackedBars = 1,

    /// <summary>One line per series.</summary>
    Lines = 2,
}

/// <summary>One value of a series.</summary>
/// <param name="Value">The number; null = nothing (a gap).</param>
/// <param name="IsApproximate">True if the number rests on incomplete data (drawn muted, "≈" in the tooltip).</param>
/// <param name="Tooltip">Series, period and value as text, shown when the point is tapped.</param>
public sealed record ChartPoint(decimal? Value, bool IsApproximate, string Tooltip);

/// <summary>A series of a chart.</summary>
/// <param name="Name">The legend text.</param>
/// <param name="Color">Colour as <c>#RRGGBB</c>.</param>
/// <param name="Points">One point per entry of <see cref="ChartModel.Periods"/>.</param>
public sealed record ChartSeries(string Name, string Color, IReadOnlyList<ChartPoint> Points);

/// <summary>A chart ready to draw (no chart-library types: the App maps it).</summary>
/// <param name="Title">What the chart shows, with its unit.</param>
/// <param name="Kind">How the series are drawn.</param>
/// <param name="Periods">The labels of the horizontal axis.</param>
/// <param name="Series">The series (at most <see cref="ReportChartBuilder.MaxSeries"/>).</param>
/// <param name="ValueKind">The kind of number (decides the axis decimals).</param>
/// <param name="Unit">The unit written after the numbers on the axis ("€", "m³", "%"); empty = none.</param>
/// <param name="HasApproximate">True if at least one point is approximate.</param>
/// <param name="ApproximateNote">Explains the muted points; null when there are none.</param>
/// <param name="PeriodNote">Says the months of a long period are charted by year; null otherwise.</param>
public sealed record ChartModel(
    string Title,
    ChartKind Kind,
    IReadOnlyList<string> Periods,
    IReadOnlyList<ChartSeries> Series,
    ReportValueKind ValueKind,
    string Unit,
    bool HasApproximate,
    string? ApproximateNote,
    string? PeriodNote = null);

/// <summary>The scale of a value axis: round numbers from <see cref="Min"/> to <see cref="Max"/> in steps of <see cref="Step"/>.</summary>
/// <param name="Min">The lowest tick (0 or below when the data reaches zero).</param>
/// <param name="Max">The highest tick.</param>
/// <param name="Step">The distance between ticks.</param>
public readonly record struct ChartScale(decimal Min, decimal Max, decimal Step)
{
    /// <summary>The tick values, lowest first.</summary>
    public IEnumerable<decimal> Ticks()
    {
        if (Step <= 0)
        {
            yield return Min;
            yield break;
        }

        for (var value = Min; value <= Max; value += Step)
        {
            yield return value;
        }
    }

    /// <summary>
    /// A scale with round ticks (1, 2, 2.5, 5 × 10ⁿ) that covers the data. Bars start at zero; lines may start above it.
    /// </summary>
    /// <param name="low">Lowest value of the data.</param>
    /// <param name="high">Highest value of the data.</param>
    /// <param name="includeZero">True for bars (the baseline is zero).</param>
    /// <param name="targetTicks">About how many ticks are wanted.</param>
    public static ChartScale For(decimal low, decimal high, bool includeZero, int targetTicks = 5)
    {
        if (includeZero)
        {
            low = Math.Min(low, 0m);
            high = Math.Max(high, 0m);
        }

        if (high <= low)
        {
            // A flat or empty data set: a range around the value.
            var pad = low == 0m ? 1m : Math.Abs(low) * 0.1m;
            low -= includeZero && low >= 0m ? 0m : pad;
            high += pad;
        }

        var step = NiceStep((high - low) / Math.Max(1, targetTicks - 1));
        var min = Math.Floor(low / step) * step;
        var max = Math.Ceiling(high / step) * step;
        return new ChartScale(min, max, step);
    }

    private static decimal NiceStep(decimal raw)
    {
        // raw > 0. Scale it into [1, 10) and take the next of 1, 2, 2.5, 5, 10.
        var magnitude = 1m;
        while (raw / magnitude >= 10m)
        {
            magnitude *= 10m;
        }

        while (raw / magnitude < 1m)
        {
            magnitude /= 10m;
        }

        var fraction = raw / magnitude;
        var nice = fraction <= 1m ? 1m : fraction <= 2m ? 2m : fraction <= 2.5m ? 2.5m : fraction <= 5m ? 5m : 10m;
        return nice * magnitude;
    }
}
