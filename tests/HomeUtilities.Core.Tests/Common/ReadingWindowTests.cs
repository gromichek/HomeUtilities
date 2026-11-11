using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Common;

public class ReadingWindowTests
{
    [Fact]
    public void Plain_window() =>
        Assert.Equal((new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 26)), ReadingWindow.For(new YearMonth(2026, 10), 20, 26));

    [Fact]
    public void Wrapping_window_ends_next_month() =>
        Assert.Equal((new DateOnly(2026, 12, 26), new DateOnly(2027, 1, 3)), ReadingWindow.For(new YearMonth(2026, 12), 26, 3));

    [Fact]
    public void Days_of_a_plain_window_are_listed_in_order() =>
        Assert.Equal([25, 26, 27, 28], ReadingWindow.Days(25, 28));

    [Fact]
    public void Days_of_a_window_crossing_the_month_end_continue_with_the_first_days() =>
        Assert.Equal([28, 29, 30, 31, 1, 2, 3], ReadingWindow.Days(28, 3));

    [Fact]
    public void Days_of_an_invalid_window_are_empty() =>
        Assert.Empty(ReadingWindow.Days(0, 3));

    [Theory]
    [InlineData(25, 28, 26, true)]
    [InlineData(25, 28, 24, false)]
    [InlineData(28, 3, 1, true)]
    [InlineData(28, 3, 31, true)]
    [InlineData(28, 3, 15, false)]
    public void Contains_checks_the_window_days(int from, int to, int day, bool expected) =>
        Assert.Equal(expected, ReadingWindow.Contains(from, to, day));

    [Theory]
    [InlineData(null, 2026, 11, 25)] // no day: first day
    [InlineData(26, 2026, 11, 26)]
    [InlineData(25, 2026, 11, 25)]
    [InlineData(28, 2026, 11, 28)]
    [InlineData(10, 2026, 11, 25)] // outside the window: first day
    public void Reminder_date_of_a_plain_window(int? day, int year, int month, int expectedDay) =>
        Assert.Equal(new DateOnly(year, month, expectedDay), ReadingWindow.ReminderDate(new YearMonth(2026, 11), 25, 28, day));

    [Theory]
    [InlineData(29, 2026, 11, 29)]
    [InlineData(1, 2026, 12, 1)] // before the start day: next month
    [InlineData(3, 2026, 12, 3)]
    [InlineData(15, 2026, 11, 28)] // outside: first day
    public void Reminder_date_of_a_window_crossing_the_month_end(int day, int year, int month, int expectedDay) =>
        Assert.Equal(new DateOnly(year, month, expectedDay), ReadingWindow.ReminderDate(new YearMonth(2026, 11), 28, 3, day));

    [Fact]
    public void Reminder_date_beyond_the_month_length_falls_on_the_last_day_of_the_month() =>
        Assert.Equal(new DateOnly(2027, 2, 28), ReadingWindow.ReminderDate(new YearMonth(2027, 2), 28, 3, 30));

    [Fact]
    public void Days_are_clamped_to_month_length() =>
        Assert.Equal((new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28)), ReadingWindow.For(new YearMonth(2026, 2), 30, 31));
}
