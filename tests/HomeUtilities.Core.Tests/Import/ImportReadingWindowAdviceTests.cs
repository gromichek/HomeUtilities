using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Core.Tests.Import;

public class ImportReadingWindowAdviceTests
{
    // A gas sheet: readings taken on the 1st, 2nd or 3rd of the month after the service month ("previous month" rule).
    private static PlannedRow Row(DateOnly date, YearMonth period, bool withReading = true)
    {
        var row = new PlannedRow { RowNumber = 2, RowDate = date, Period = period };
        if (withReading)
        {
            row.Readings.Add(new PlannedReading(Guid.NewGuid(), new MeterReading { Date = date, Value = 10m }));
        }

        return row;
    }

    [Fact]
    public void Readings_on_the_1st_without_a_window_suggest_28_to_4()
    {
        // 01.09.2026 -> period 08.2026; 02.10.2026 -> 09.2026; 01.11.2026 -> 10.2026; latest day 2, +2 = 4
        var rows = new[]
        {
            Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 8)),
            Row(new DateOnly(2026, 10, 2), new YearMonth(2026, 9)),
            Row(new DateOnly(2026, 11, 1), new YearMonth(2026, 10)),
        };

        var suggestion = ImportReadingWindowAdvice.Suggest(rows, new Contract());

        Assert.Equal(new ReadingWindowSuggestion(28, 4, 3), suggestion);
    }

    [Fact]
    public void The_end_day_is_the_latest_day_plus_two_but_at_most_10()
    {
        var late = new[] { Row(new DateOnly(2026, 9, 9), new YearMonth(2026, 8)) };

        Assert.Equal(10, ImportReadingWindowAdvice.Suggest(late, new Contract())!.To);
    }

    [Fact]
    public void A_window_that_contains_the_dates_needs_no_suggestion()
    {
        // window 28-5: the window of period 08.2026 is 28.08.2026 - 05.09.2026 and contains 01.09.2026
        var contract = new Contract { ReadingDayFrom = 28, ReadingDayTo = 5 };

        Assert.Null(ImportReadingWindowAdvice.Suggest([Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 8))], contract));
    }

    [Fact]
    public void A_window_that_does_not_contain_the_dates_still_gets_a_suggestion()
    {
        // water window 25-28 does not contain the 1st
        var contract = new Contract { ReadingDayFrom = 25, ReadingDayTo = 28 };

        var suggestion = ImportReadingWindowAdvice.Suggest([Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 8))], contract);

        Assert.Equal(new ReadingWindowSuggestion(28, 3, 1), suggestion);
    }

    [Fact]
    public void Rows_in_their_own_month_without_readings_with_errors_or_corrected_are_ignored()
    {
        var sameMonth = Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 9));
        var noReading = Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 8), withReading: false);
        var failed = Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 8));
        failed.Issues.Add(new ImportIssue(ImportIssueSeverity.Error, ImportIssueCodes.NumberInvalid, []));
        var corrected = Row(new DateOnly(2026, 9, 1), new YearMonth(2026, 8));
        corrected.Override = new ImportRowOverride { RowDate = new DateOnly(2026, 9, 1), Period = new DateOnly(2026, 8, 1) };
        var mid = Row(new DateOnly(2026, 9, 15), new YearMonth(2026, 8));

        Assert.Null(ImportReadingWindowAdvice.Suggest([sameMonth, noReading, failed, corrected, mid], new Contract()));
    }
}
