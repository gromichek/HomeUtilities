using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tests.Fakes;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Consumption;

public class ConsumptionHistoryTests
{
    private static MeterChain Chain(params Meter[] meters) => new(meters);

    /// <summary>A meter read on the first of every month from 2025-10 on with the given monthly consumptions.</summary>
    private static Meter MonthlyMeter(params decimal[] consumptions)
    {
        var meter = Meter(D(2025, 10, 1));
        var value = 100m;
        var date = new YearMonth(2025, 10);
        meter.Add(date.FirstDay, value);
        foreach (var consumption in consumptions)
        {
            date = date.AddMonths(1);
            value += consumption;
            meter.Add(date.FirstDay, value);
        }

        return meter;
    }

    [Fact]
    public void Twelve_months_oldest_first()
    {
        // Period P uses the latest reading on/before its last day: the one on the 1st of month P.
        // Readings 2025-10 .. 2026-10: 100, 110, 121, 133, 146, 160, 175, 191, 208, 226, 245, 265, 286
        var meter = MonthlyMeter(10m, 11m, 12m, 13m, 14m, 15m, 16m, 17m, 18m, 19m, 20m, 21m);

        var history = ConsumptionHistory.Monthly(Chain(meter), RegisterKind.Total, new YearMonth(2026, 9), 12, null);

        // 2025-10 has no start reading and is skipped; 2025-11 = 110 - 100 = 10 ... 2026-09 = 265 - 245 = 20
        Assert.Equal([10m, 11m, 12m, 13m, 14m, 15m, 16m, 17m, 18m, 19m, 20m], history);
    }

    [Fact]
    public void Gaps_are_skipped()
    {
        var meter = Meter(D(2026, 1, 1));
        meter.Add(D(2026, 1, 31), 100m);
        meter.Add(D(2026, 2, 28), 110m);
        // March has no reading but the latest-on-or-before rule reuses February, so March is 0 and April picks it up
        meter.Add(D(2026, 4, 30), 130m);

        var history = ConsumptionHistory.Monthly(Chain(meter), RegisterKind.Total, new YearMonth(2026, 4), 4, null);

        // Jan: no start reading (skipped); Feb: 110-100 = 10; Mar: 110-110 = 0; Apr: 130-110 = 20
        Assert.Equal([10m, 0m, 20m], history);
    }

    [Fact]
    public void Negative_consumption_without_digits_is_skipped()
    {
        var meter = Meter(D(2026, 1, 1));
        meter.Add(D(2026, 1, 31), 500m);
        meter.Add(D(2026, 2, 28), 100m);
        meter.Add(D(2026, 3, 31), 112m);

        var history = ConsumptionHistory.Monthly(Chain(meter), RegisterKind.Total, new YearMonth(2026, 3), 3, null);

        // Feb is negative (100 - 500) and skipped, Mar = 112 - 100 = 12
        Assert.Equal([12m], history);
    }

    [Fact]
    public void Replacement_inside_the_history_is_bridged()
    {
        var old = Meter(D(2020, 1, 1));
        old.Add(D(2026, 1, 31), 1000m);
        old.Add(D(2026, 2, 28), 1010m);
        old.Add(D(2026, 3, 15), 1015m, ReadingKind.Final);
        var next = Meter(D(2026, 3, 15), replaced: old.Id);
        next.Add(D(2026, 3, 15), 0m, ReadingKind.Initial);
        next.Add(D(2026, 3, 31), 4m);
        next.Add(D(2026, 4, 30), 13m);

        var history = ConsumptionHistory.Monthly(Chain(old, next), RegisterKind.Total, new YearMonth(2026, 4), 3, null);

        // Feb: 1010 - 1000 = 10; Mar: (1015 - 1010) + (4 - 0) = 9; Apr: 13 - 4 = 9
        Assert.Equal([10m, 9m, 9m], history);
    }

    [Fact]
    public void Reading_window_is_respected()
    {
        var meter = Meter(D(2026, 1, 1));
        meter.Add(D(2026, 1, 26), 100m);
        meter.Add(D(2026, 2, 26), 107m);
        meter.Add(D(2026, 3, 27), 119m);

        var history = ConsumptionHistory.Monthly(Chain(meter), RegisterKind.Total, new YearMonth(2026, 3), 2, (25, 28));

        // Feb: 107 - 100 = 7; Mar: 119 - 107 = 12
        Assert.Equal([7m, 12m], history);
    }

    [Fact]
    public void Zero_months_gives_an_empty_list() =>
        Assert.Empty(ConsumptionHistory.Monthly(Chain(MonthlyMeter(5m)), RegisterKind.Total, new YearMonth(2026, 1), 0, null));
}
