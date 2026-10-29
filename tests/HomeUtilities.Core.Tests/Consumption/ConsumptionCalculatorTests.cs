using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Consumption;

public class ConsumptionCalculatorTests
{
    private static MeterChain Chain(params Meter[] meters) => new(meters);

    private static string[] Codes(ConsumptionResult r) => [.. r.Warnings.Select(w => w.Code)];

    [Fact]
    public void Simple_consumption()
    {
        // 425.900 - 413.816 = 12.084
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 9, 24), 413.816m);
        m.Add(D(2026, 10, 25), 425.900m);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 9, 30), D(2026, 10, 31));

        Assert.True(r.IsComplete);
        Assert.Equal(12.084m, r.Quantity);
        Assert.Equal(413.816m, r.StartReading!.Value);
        Assert.Equal(425.900m, r.EndReading!.Value);
        var s = Assert.Single(r.Segments);
        Assert.Equal((m.Id, 12.084m, false), (s.MeterId, s.Quantity, s.Rollover));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Readings_on_boundary_dates_count_and_latest_wins()
    {
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 1, 1), 100m);
        m.Add(D(2026, 1, 10), 110m);
        m.Add(D(2026, 2, 1), 120m);
        m.Add(D(2026, 2, 2), 999m);

        // start = latest on or before 2026-01-15 -> 110; end = on or before 2026-02-01 -> 120; 10
        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 15), D(2026, 2, 1));
        Assert.Equal(10m, r.Quantity);
    }

    [Fact]
    public void Deleted_readings_are_ignored()
    {
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 1, 1), 100m);
        m.Add(D(2026, 1, 20), 500m).IsDeleted = true;
        m.Add(D(2026, 2, 1), 130m);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 31), D(2026, 2, 1));
        Assert.Equal(30m, r.Quantity);
        Assert.Equal(100m, r.StartReading!.Value);
    }

    [Fact]
    public void Rollover_with_digits()
    {
        // 12 + 10^5 - 99950 = 62
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 1, 1), 99950m);
        m.Add(D(2026, 2, 1), 12m);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1));

        Assert.True(r.IsComplete);
        Assert.Equal(62m, r.Quantity);
        Assert.True(r.Segments[0].Rollover);
        Assert.Equal([ConsumptionWarningCodes.RolloverAssumed], Codes(r));
    }

    [Fact]
    public void Negative_without_digits_is_incomplete()
    {
        var m = Meter(D(2015, 1, 1), null);
        m.Add(D(2026, 1, 1), 99950m);
        m.Add(D(2026, 2, 1), 12m);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1));

        Assert.False(r.IsComplete);
        Assert.Equal(0m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.NegativeConsumption], Codes(r));
    }

    private static (Meter Old, Meter New) ReplacedPair()
    {
        var old = Meter(D(2009, 1, 1), 5);
        old.Add(D(2015, 3, 1), 190m);
        old.Add(D(2015, 4, 30), 202m, ReadingKind.Final);
        old.RemovedOn = D(2015, 4, 30);
        var fresh = Meter(D(2015, 4, 30), 5, old.Id);
        fresh.Add(D(2015, 4, 30), 0m, ReadingKind.Initial);
        return (old, fresh);
    }

    [Fact]
    public void Replacement_inside_interval_sums_segments()
    {
        // old: 202 - 190 = 12; new: 13 - 0 = 13; total 25
        var (old, fresh) = ReplacedPair();
        fresh.Add(D(2015, 6, 1), 13m);

        var r = ConsumptionCalculator.Calculate(Chain(old, fresh), RegisterKind.Total, D(2015, 3, 1), D(2015, 6, 1));

        Assert.True(r.IsComplete);
        Assert.Equal(25m, r.Quantity);
        Assert.Equal([12m, 13m], r.Segments.Select(s => s.Quantity));
        Assert.Equal([old.Id, fresh.Id], r.Segments.Select(s => s.MeterId));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Start_on_replacement_day_uses_new_meter()
    {
        var (old, fresh) = ReplacedPair();
        fresh.Add(D(2015, 6, 1), 13m);

        var r = ConsumptionCalculator.Calculate(Chain(old, fresh), RegisterKind.Total, D(2015, 4, 30), D(2015, 6, 1));

        Assert.Equal(13m, r.Quantity);
        Assert.Single(r.Segments);
    }

    [Fact]
    public void Two_replacements_in_one_interval()
    {
        // a: 50 - 40 = 10; b: 30 - 0 = 30; c: 7 - 1 = 6; total 46
        var a = Meter(D(2000, 1, 1), 5);
        a.Add(D(2020, 1, 1), 40m);
        a.Add(D(2020, 6, 1), 50m, ReadingKind.Final);
        var b = Meter(D(2020, 6, 1), 5, a.Id);
        b.Add(D(2020, 6, 1), 0m, ReadingKind.Initial);
        b.Add(D(2021, 6, 1), 30m, ReadingKind.Final);
        var c = Meter(D(2021, 6, 1), 5, b.Id);
        c.Add(D(2021, 6, 1), 1m, ReadingKind.Initial);
        c.Add(D(2021, 12, 1), 7m);

        var r = ConsumptionCalculator.Calculate(MeterChain.Build([a, b, c], b.Id), RegisterKind.Total, D(2020, 1, 1), D(2021, 12, 1));

        Assert.Equal(46m, r.Quantity);
        Assert.Equal([10m, 30m, 6m], r.Segments.Select(s => s.Quantity));
    }

    [Fact]
    public void Replaced_meter_without_final_is_incomplete()
    {
        var (old, fresh) = ReplacedPair();
        old.Registers[0].Readings.RemoveAll(r => r.Kind == ReadingKind.Final);
        fresh.Add(D(2015, 6, 1), 13m);

        var r = ConsumptionCalculator.Calculate(Chain(old, fresh), RegisterKind.Total, D(2015, 3, 1), D(2015, 6, 1));

        Assert.False(r.IsComplete);
        Assert.Equal(0m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.MissingFinalReading], Codes(r));
    }

    [Fact]
    public void An_estimated_reading_on_the_removal_date_ends_a_replaced_meter()
    {
        // The import stores an estimated final when the sheet gave none: old 202 - 190 = 12; new 13 - 0 = 13.
        var (old, fresh) = ReplacedPair();
        old.Registers[0].Readings.Single(r => r.Kind == ReadingKind.Final).Kind = ReadingKind.Estimated;
        fresh.Add(D(2015, 6, 1), 13m);

        var r = ConsumptionCalculator.Calculate(Chain(old, fresh), RegisterKind.Total, D(2015, 3, 1), D(2015, 6, 1));

        Assert.True(r.IsComplete);
        Assert.Equal(25m, r.Quantity);
    }

    [Fact]
    public void An_estimated_reading_inside_the_life_of_a_replaced_meter_is_not_its_end()
    {
        var (old, fresh) = ReplacedPair();
        old.Registers[0].Readings.RemoveAll(r => r.Kind == ReadingKind.Final);
        old.Add(D(2015, 4, 1), 199m, ReadingKind.Estimated);
        fresh.Add(D(2015, 6, 1), 13m);

        var r = ConsumptionCalculator.Calculate(Chain(old, fresh), RegisterKind.Total, D(2015, 3, 1), D(2015, 6, 1));

        Assert.False(r.IsComplete);
        Assert.Equal([ConsumptionWarningCodes.MissingFinalReading], Codes(r));
    }

    [Fact]
    public void Dual_tariff_registers_are_independent()
    {
        var m = Meter(D(2020, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night);
        m.Add(D(2026, 1, 1), 1000m, register: RegisterKind.Day);
        m.Add(D(2026, 2, 1), 1180m, register: RegisterKind.Day);
        m.Add(D(2026, 1, 1), 400m, register: RegisterKind.Night);
        m.Add(D(2026, 2, 1), 450m, register: RegisterKind.Night);

        var day = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Day, D(2026, 1, 1), D(2026, 2, 1));
        var night = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Night, D(2026, 1, 1), D(2026, 2, 1));

        Assert.Equal(180m, day.Quantity);
        Assert.Equal(50m, night.Quantity);
    }

    [Fact]
    public void Solar_export_register()
    {
        // 1450 - 1200 = 250 kWh
        var m = Meter(D(2022, 1, 1), 7, null, RegisterKind.Day, RegisterKind.Export);
        m.Add(D(2026, 1, 1), 1200m, register: RegisterKind.Export);
        m.Add(D(2026, 2, 1), 1450m, register: RegisterKind.Export);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Export, D(2026, 1, 1), D(2026, 2, 1));
        Assert.Equal(250m, r.Quantity);
    }

    [Fact]
    public void Missing_start_reading_is_incomplete()
    {
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 2, 1), 120m);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1));

        Assert.False(r.IsComplete);
        Assert.Equal(0m, r.Quantity);
        Assert.Null(r.StartReading);
        Assert.Equal([ConsumptionWarningCodes.MissingStartReading], Codes(r));
    }

    [Fact]
    public void Missing_end_reading_is_incomplete()
    {
        var m = Meter(D(2015, 1, 1), 5);
        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1));

        Assert.False(r.IsComplete);
        Assert.Equal([ConsumptionWarningCodes.MissingStartReading, ConsumptionWarningCodes.MissingEndReading], Codes(r));
    }

    [Fact]
    public void Estimated_reading_is_used_with_warning_and_control_is_not()
    {
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 1, 1), 100m, ReadingKind.Control);
        m.Add(D(2026, 2, 1), 110m, ReadingKind.Estimated);

        var r = ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1));
        Assert.Equal(10m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.EstimatedReadingUsed], Codes(r));

        var m2 = Meter(D(2015, 1, 1), 5);
        m2.Add(D(2026, 1, 1), 100m, ReadingKind.Control);
        m2.Add(D(2026, 2, 1), 110m);
        Assert.Empty(ConsumptionCalculator.Calculate(Chain(m2), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1)).Warnings);
    }

    [Fact]
    public void Quantity_is_rounded_to_three_decimals()
    {
        var m = Meter(D(2015, 1, 1), 5);
        m.Add(D(2026, 1, 1), 1.0004m);
        m.Add(D(2026, 2, 1), 2.0009m);

        // 1.0005 -> 1.001 (away from zero)
        Assert.Equal(1.001m, ConsumptionCalculator.Calculate(Chain(m), RegisterKind.Total, D(2026, 1, 1), D(2026, 2, 1)).Quantity);
    }

    [Fact]
    public void To_before_from_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ConsumptionCalculator.Calculate(Chain(Meter(D(2015, 1, 1))), RegisterKind.Total, D(2026, 2, 1), D(2026, 1, 1)));
    }

    private static Meter Water()
    {
        var m = Meter(D(2015, 1, 1), 6);
        m.Add(D(2026, 9, 24), 437.097m);
        return m;
    }

    [Fact]
    public void Period_with_window_picks_readings_inside_window()
    {
        // 445.000 - 437.097 = 7.903
        var m = Water();
        m.Add(D(2026, 10, 25), 445.000m);
        m.Add(D(2026, 10, 28), 446.000m);

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (20, 26));

        Assert.Equal(7.903m, r.Quantity);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Period_with_window_prefers_late_reading_over_early_one()
    {
        // no reading in 2026-10-20..26; the earliest reading in 2026-10-27..2026-11-19 is 2026-10-28: 446.000 - 437.097 = 8.903
        var m = Water();
        m.Add(D(2026, 10, 18), 441.500m);
        m.Add(D(2026, 10, 28), 446.000m);

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (20, 26));

        Assert.True(r.IsComplete);
        Assert.Equal(8.903m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.ReadingOutsideWindow], Codes(r));
    }

    [Fact]
    public void Period_with_window_uses_late_reading()
    {
        // 445.000 - 437.097 = 7.903
        var m = Water();
        m.Add(D(2026, 10, 28), 445.000m);

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (20, 26));

        Assert.Equal(7.903m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.ReadingOutsideWindow], Codes(r));
    }

    [Fact]
    public void Period_with_window_uses_early_reading_when_no_late_one()
    {
        // 441.000 - 437.097 = 3.903
        var m = Water();
        m.Add(D(2026, 10, 15), 441.000m);

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (20, 26));

        Assert.Equal(3.903m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.ReadingOutsideWindow], Codes(r));
    }

    [Fact]
    public void Period_with_window_does_not_reuse_the_start_reading_as_end_reading()
    {
        var m = Water();

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (20, 26));

        Assert.False(r.IsComplete);
        Assert.Equal(0m, r.Quantity);
        Assert.Equal([ConsumptionWarningCodes.MissingEndReading], Codes(r));
    }

    [Fact]
    public void Late_reading_is_not_stolen_from_the_next_period()
    {
        var m = Water();
        m.Add(D(2026, 10, 28), 445.000m);
        m.Add(D(2026, 11, 22), 452.000m);

        var october = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (20, 26));
        var november = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 11), (20, 26));

        Assert.Equal(D(2026, 10, 28), october.EndReading!.Date);
        Assert.Equal(7.903m, october.Quantity);
        Assert.Equal(D(2026, 10, 28), november.StartReading!.Date);
        Assert.Equal(D(2026, 11, 22), november.EndReading!.Date);
        Assert.Equal(7m, november.Quantity);
        Assert.Equal([ConsumptionWarningCodes.ReadingOutsideWindow], Codes(november));
    }

    [Fact]
    public void Period_without_window_uses_readings_on_or_before_month_end()
    {
        // end: 2026-10-28 (446.000) on or before 2026-10-31; start: 2026-09-24 (437.097) on or before 2026-09-30
        var m = Water();
        m.Add(D(2026, 10, 28), 446.000m);
        m.Add(D(2026, 11, 2), 450.000m);

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), null);

        Assert.Equal(8.903m, r.Quantity);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Period_window_wrapping_month_end()
    {
        // window 28..3: period 2026-10 -> 2026-10-28..2026-11-03; period 2026-09 -> 2026-09-28..2026-10-03
        var m = Meter(D(2015, 1, 1), 6);
        m.Add(D(2026, 10, 1), 100m);
        m.Add(D(2026, 11, 2), 112m);

        var r = ConsumptionCalculator.CalculateForPeriod(Chain(m), RegisterKind.Total, new YearMonth(2026, 10), (28, 3));

        Assert.Equal(12m, r.Quantity);
        Assert.Empty(r.Warnings);
    }
}
