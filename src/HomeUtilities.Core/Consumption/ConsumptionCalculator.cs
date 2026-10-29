using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Consumption;

/// <summary>Turns meter readings into consumption, handling rollover, meter replacement and gaps.</summary>
public static class ConsumptionCalculator
{
    private sealed record Located(MeterReading Reading, int MeterIndex);

    /// <summary>
    /// Consumption between the latest reading on or before <paramref name="from"/> and the latest reading
    /// on or before <paramref name="to"/> over the register of the given kind.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="to"/> is before <paramref name="from"/>.</exception>
    public static ConsumptionResult Calculate(MeterChain chain, RegisterKind kind, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (to < from)
        {
            throw new ArgumentException("The end date is before the start date.", nameof(to));
        }

        var readings = LocateReadings(chain, kind);
        return Compute(chain, kind, LatestOnOrBefore(readings, from), LatestOnOrBefore(readings, to), []);
    }

    /// <summary>
    /// Consumption of a month: between the readings assigned to the previous period and to <paramref name="period"/>.
    /// With a reading window (days of month from/to) the latest reading inside the window of a period is used;
    /// if there is none, the earliest reading between the window end and the next period's window start (a late
    /// reading) or else the latest reading before the window start (an early reading) is used with
    /// <see cref="ConsumptionWarningCodes.ReadingOutsideWindow"/>; an early reading that already serves as the
    /// start reading is not used again. Without a window the latest reading on or before
    /// the last day of the period is used.
    /// </summary>
    public static ConsumptionResult CalculateForPeriod(
        MeterChain chain,
        RegisterKind kind,
        YearMonth period,
        (int From, int To)? readingWindowDays)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (readingWindowDays is not { } days)
        {
            return Calculate(chain, kind, period.AddMonths(-1).LastDay, period.LastDay);
        }

        var readings = LocateReadings(chain, kind);
        var warnings = new List<ConsumptionWarning>();
        var start = PickForWindow(readings, period.AddMonths(-1), days, null, warnings);
        var end = PickForWindow(readings, period, days, start, warnings);
        return Compute(chain, kind, start, end, warnings);
    }

    private static Located? PickForWindow(List<Located> readings, YearMonth period, (int From, int To) days, Located? excluded, List<ConsumptionWarning> warnings)
    {
        var (from, to) = ReadingWindow.For(period, days.From, days.To);
        var inside = readings.LastOrDefault(r => r.Reading.Date >= from && r.Reading.Date <= to);
        if (inside is not null)
        {
            return inside;
        }

        var nextFrom = ReadingWindow.For(period.AddMonths(1), days.From, days.To).From;
        var picked = readings.FirstOrDefault(r => r.Reading.Date > to && r.Reading.Date < nextFrom)
            ?? readings.LastOrDefault(r => r.Reading.Date < from);
        if (picked is null || picked == excluded)
        {
            return null;
        }

        warnings.Add(new(ConsumptionWarningCodes.ReadingOutsideWindow, [period.ToString()]));
        return picked;
    }

    /// <summary>Readings of the register of the given kind over all meters of the chain, ordered by date, then by meter position.</summary>
    private static List<Located> LocateReadings(MeterChain chain, RegisterKind kind) =>
        [.. chain.Meters
            .SelectMany((meter, index) => RegisterReadings(meter, kind).Select(r => new Located(r, index)))
            .OrderBy(l => l.Reading.Date)
            .ThenBy(l => l.MeterIndex)];

    private static List<MeterReading> RegisterReadings(Meter meter, RegisterKind kind) =>
        [.. meter.Registers
            .Where(r => !r.IsDeleted && r.Kind == kind)
            .SelectMany(r => r.Readings)
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Date)];

    // The last reading of a replaced meter: its Final reading, or the estimated one the import stored on the removal date.
    private static MeterReading? LastReadingOf(Meter meter, List<MeterReading> own) =>
        own.LastOrDefault(r => r.Kind == ReadingKind.Final)
        ?? own.LastOrDefault(r => r.Kind == ReadingKind.Estimated && r.Date == meter.RemovedOn);

    private static Located? LatestOnOrBefore(List<Located> readings, DateOnly date) =>
        readings.LastOrDefault(r => r.Reading.Date <= date);

    private static ConsumptionResult Compute(MeterChain chain, RegisterKind kind, Located? start, Located? end, List<ConsumptionWarning> warnings)
    {
        var segments = new List<ConsumptionSegment>();
        var complete = true;

        if (start is null)
        {
            warnings.Add(new(ConsumptionWarningCodes.MissingStartReading));
            complete = false;
        }

        if (end is null)
        {
            warnings.Add(new(ConsumptionWarningCodes.MissingEndReading));
            complete = false;
        }

        if (start is null || end is null)
        {
            return Finish(start, end, segments, warnings, false);
        }

        if (start.MeterIndex > end.MeterIndex)
        {
            warnings.Add(new(ConsumptionWarningCodes.NegativeConsumption));
            return Finish(start, end, segments, warnings, false);
        }

        for (var i = start.MeterIndex; i <= end.MeterIndex && complete; i++)
        {
            var meter = chain.Meters[i];
            var own = RegisterReadings(meter, kind);

            MeterReading? segStart = i == start.MeterIndex ? start.Reading : FirstReading(own);
            MeterReading? segEnd = i == end.MeterIndex ? end.Reading : LastReadingOf(meter, own);

            if (segStart is null)
            {
                warnings.Add(new(ConsumptionWarningCodes.MissingInitialReading, [meter.Id]));
                complete = false;
            }
            else if (segEnd is null)
            {
                warnings.Add(new(ConsumptionWarningCodes.MissingFinalReading, [meter.Id]));
                complete = false;
            }
            else
            {
                complete = AddSegment(segments, warnings, meter, segStart, segEnd);
            }
        }

        return Finish(start, end, segments, warnings, complete);
    }

    private static MeterReading? FirstReading(List<MeterReading> readings) =>
        readings.FirstOrDefault(r => r.Kind == ReadingKind.Initial) ?? readings.FirstOrDefault();

    private static bool AddSegment(List<ConsumptionSegment> segments, List<ConsumptionWarning> warnings, Meter meter, MeterReading startReading, MeterReading endReading)
    {
        var rollover = false;
        var quantity = endReading.Value - startReading.Value;
        if (quantity < 0)
        {
            if (meter.Digits is not { } digits)
            {
                warnings.Add(new(ConsumptionWarningCodes.NegativeConsumption, [meter.Id]));
                return false;
            }

            quantity += Pow10(digits);
            rollover = true;
            warnings.Add(new(ConsumptionWarningCodes.RolloverAssumed, [meter.Id]));
        }

        segments.Add(new(meter.Id, startReading.Value, endReading.Value, MoneyMath.RoundQuantity(quantity), rollover));
        return true;
    }

    private static ConsumptionResult Finish(Located? start, Located? end, List<ConsumptionSegment> segments, List<ConsumptionWarning> warnings, bool complete)
    {
        if (complete)
        {
            if (start?.Reading.Kind == ReadingKind.Estimated || end?.Reading.Kind == ReadingKind.Estimated)
            {
                warnings.Add(new(ConsumptionWarningCodes.EstimatedReadingUsed));
            }
        }
        else
        {
            segments = [];
        }

        var quantity = complete ? MoneyMath.RoundQuantity(segments.Sum(s => s.Quantity)) : 0m;
        return new ConsumptionResult(quantity, start?.Reading, end?.Reading, segments, warnings, complete);
    }

    private static decimal Pow10(int digits)
    {
        var result = 1m;
        for (var i = 0; i < digits; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
