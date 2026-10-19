using HomeUtilities.Core;

namespace HomeUtilities.Core.Tests;

public class SystemClockTests
{
    [Fact]
    public void Today_EqualsLocalDate()
    {
        var clock = new SystemClock();

        var before = DateOnly.FromDateTime(DateTime.Now);
        var today = clock.Today;
        var after = DateOnly.FromDateTime(DateTime.Now);

        Assert.InRange(today, before, after);
    }

    [Fact]
    public void UtcNow_IsUtcAndCloseToSystemTime()
    {
        var clock = new SystemClock();

        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.InRange(now, before, after);
    }
}
