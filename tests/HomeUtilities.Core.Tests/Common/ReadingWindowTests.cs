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
    public void Days_are_clamped_to_month_length() =>
        Assert.Equal((new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28)), ReadingWindow.For(new YearMonth(2026, 2), 30, 31));
}
