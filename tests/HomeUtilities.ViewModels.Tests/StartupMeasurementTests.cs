namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-041: the cold start is measured from the process start only when the first window followed it shortly.</summary>
public class StartupMeasurementTests
{
    private static DateTimeOffset At(int hour, int minute, int second, int millisecond = 0) =>
        new(2026, 11, 9, hour, minute, second, millisecond, TimeSpan.Zero);

    [Fact]
    public void WindowOneSecondAfterTheProcess_IsMeasuredFromTheProcess()
    {
        // process 10:00:00, window 10:00:01, Overview 10:00:03.3 -> 3 300 ms
        var origin = PerformanceLog.StartupOrigin(At(10, 0, 0), At(10, 0, 1));

        Assert.Equal(TimeSpan.FromMilliseconds(3300), At(10, 0, 3, 300) - origin);
    }

    [Fact]
    public void ProcessStartedAnHourBeforeTheWindow_IsMeasuredFromTheWindow()
    {
        // process 09:00:00 (alarm, no window), window 10:00:00, Overview 10:00:02 -> 2 000 ms
        var origin = PerformanceLog.StartupOrigin(At(9, 0, 0), At(10, 0, 0));

        Assert.Equal(TimeSpan.FromMilliseconds(2000), At(10, 0, 2) - origin);
    }

    [Fact]
    public void ExactlyThirtySeconds_StillCountsFromTheProcess_AndOneMillisecondMoreDoesNot()
    {
        Assert.Equal(At(10, 0, 0), PerformanceLog.StartupOrigin(At(10, 0, 0), At(10, 0, 30)));
        Assert.Equal(At(10, 0, 30, 1), PerformanceLog.StartupOrigin(At(10, 0, 0), At(10, 0, 30, 1)));
    }

    [Fact]
    public void WithoutAWindowTime_TheProcessStartIsUsed()
    {
        Assert.Equal(At(10, 0, 0), PerformanceLog.StartupOrigin(At(10, 0, 0), null));
    }
}
