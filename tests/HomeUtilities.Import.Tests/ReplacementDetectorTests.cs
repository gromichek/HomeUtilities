using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ReplacementDetectorTests
{
    private static List<ReadingPoint> Monthly(params decimal[] values) =>
        [.. values.Select((v, i) => new ReadingPoint(new DateOnly(2004, 1, 26).AddMonths(i), v, i + 2))];

    private static ReadingPoint Next(List<ReadingPoint> history, decimal value, int dayOffset = 31) =>
        new(history[^1].Date.AddDays(dayOffset), value, history[^1].RowNumber + 1);

    [Fact]
    public void A_normal_step_is_not_a_replacement()
    {
        var history = Monthly(100, 104, 108, 112);

        Assert.Null(ReplacementDetector.Detect(history, Next(history, 116), null));
    }

    [Fact]
    public void The_tolerance_is_five_times_the_median_step_but_at_least_fifty()
    {
        Assert.Equal(50m, ReplacementDetector.MaxPlausibleStep(Monthly(100, 104, 108, 112)));
        // Median step 30 → tolerance 150.
        Assert.Equal(150m, ReplacementDetector.MaxPlausibleStep(Monthly(1000, 1030, 1060, 1090)));
    }

    [Fact]
    public void A_lower_value_is_a_drop()
    {
        var history = Monthly(100, 104, 108, 399);

        var found = ReplacementDetector.Detect(history, Next(history, 6), null);

        Assert.Equal(new DetectedReplacement(ReplacementSign.Drop, false), found);
    }

    [Fact]
    public void A_jump_above_five_median_steps_is_a_replacement()
    {
        var history = Monthly(100, 104, 108, 112);

        Assert.Equal(ReplacementSign.Jump, ReplacementDetector.Detect(history, Next(history, 163), null)!.Sign);
        Assert.Null(ReplacementDetector.Detect(history, Next(history, 162), null));
    }

    [Fact]
    public void A_big_consumer_may_step_further()
    {
        // Median step 30: up to 112+... → previous 1090, tolerance 150 → 1240 is still normal, 1241 is a jump.
        var history = Monthly(1000, 1030, 1060, 1090);

        Assert.Null(ReplacementDetector.Detect(history, Next(history, 1240), null));
        Assert.NotNull(ReplacementDetector.Detect(history, Next(history, 1241), null));
    }

    [Fact]
    public void A_drop_that_is_a_plausible_rollover_of_the_dial_is_not_a_replacement()
    {
        // A 4-digit dial: 9998 → 3 means 5 units used (3 + 10000 − 9998).
        var history = Monthly(9990, 9994, 9998);

        Assert.Null(ReplacementDetector.Detect(history, Next(history, 3), 4));
        Assert.NotNull(ReplacementDetector.Detect(history, Next(history, 3), null));
    }

    [Fact]
    public void A_drop_far_from_the_end_of_the_dial_is_a_replacement_even_with_digits()
    {
        var history = Monthly(100, 104, 108, 399);

        Assert.NotNull(ReplacementDetector.Detect(history, Next(history, 6), 4));
    }

    [Fact]
    public void An_extra_row_in_the_same_month_is_the_installation_day()
    {
        var history = new List<ReadingPoint>
        {
            new(new DateOnly(2004, 3, 1), 108, 4),
            new(new DateOnly(2004, 4, 1), 112, 5),
        };

        var found = ReplacementDetector.Detect(history, new ReadingPoint(new DateOnly(2004, 4, 7), 3, 6), null);

        Assert.Equal(new DetectedReplacement(ReplacementSign.Drop, true), found);
    }

    [Fact]
    public void Two_dates_in_one_month_without_a_drop_are_not_a_replacement()
    {
        // The switch from the 1st to the 26th.
        var history = new List<ReadingPoint> { new(new DateOnly(2004, 5, 1), 108, 4), new(new DateOnly(2004, 6, 1), 112, 5) };

        Assert.Null(ReplacementDetector.Detect(history, new ReadingPoint(new DateOnly(2004, 6, 26), 115, 6), null));
    }

    [Fact]
    public void Nothing_is_detected_without_a_previous_reading() =>
        Assert.Null(ReplacementDetector.Detect([], new ReadingPoint(new DateOnly(2004, 1, 1), 5, 2), null));

    [Fact]
    public void Earlier_replacements_do_not_distort_the_median()
    {
        // 100 → 399 → 6 → 10: the negative step is ignored, the median is 4.
        var history = Monthly(100, 104, 108, 399, 6, 10);

        Assert.Equal(50m, ReplacementDetector.MaxPlausibleStep(history));
        Assert.Null(ReplacementDetector.Detect(history, Next(history, 14), null));
    }
}

public class ReplacementNoteParserTests
{
    [Fact]
    public void A_date_in_a_note_is_found()
    {
        var note = ReplacementNoteParser.Parse("поменяли счетчики 30.04.2015");

        Assert.Equal(new DateOnly(2015, 4, 30), note.Date);
        Assert.Empty(note.Meters);
    }

    [Fact]
    public void A_latvian_note_gives_serial_final_and_initial_for_the_named_meter()
    {
        var note = ReplacementNoteParser.Parse("skaititaja nr.: Auksta - 1210007 rad:398,054 rad:0,072");

        var entry = Assert.Single(note.Meters);
        Assert.Equal(MeterResource.ColdWater, entry.Resource);
        Assert.Equal("1210007", entry.SerialNumber);
        Assert.Equal(398.054m, entry.OldFinal);
        Assert.Equal(0.072m, entry.NewInitial);
    }

    [Fact]
    public void Cold_and_hot_entries_are_separated()
    {
        var note = ReplacementNoteParser.Parse("Auksta - 1210007 rad:398.054 rad:0.072; Karsta - 1210008 rad:120,5 rad:0,001 01.05.2016");

        Assert.Equal(new DateOnly(2016, 5, 1), note.Date);
        Assert.Equal(2, note.Meters.Count);
        Assert.Equal((MeterResource.ColdWater, 398.054m, 0.072m), (note.Meters[0].Resource!.Value, note.Meters[0].OldFinal!.Value, note.Meters[0].NewInitial!.Value));
        Assert.Equal((MeterResource.HotWater, "1210008", 120.5m), (note.Meters[1].Resource!.Value, note.Meters[1].SerialNumber, note.Meters[1].OldFinal!.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tariff changed")]
    [InlineData("32.13.2015 no date here")]
    public void Texts_without_information_give_an_empty_note(string? text) =>
        Assert.True(ReplacementNoteParser.Parse(text).IsEmpty);

    [Fact]
    public void A_two_digit_year_is_understood() =>
        Assert.Equal(new DateOnly(2015, 4, 30), ReplacementNoteParser.Parse("replaced 30.04.15").Date);
}
