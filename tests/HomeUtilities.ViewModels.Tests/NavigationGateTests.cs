namespace HomeUtilities.ViewModels.Tests;

public class NavigationGateTests
{
    [Fact]
    public async Task SecondCallWhileFirstIsPending_IsIgnored()
    {
        var gate = new NavigationGate();
        var release = new TaskCompletionSource();
        var calls = 0;

        var first = gate.TryRunAsync(() => { calls++; return release.Task; });
        var second = await gate.TryRunAsync(() => { calls++; return Task.CompletedTask; });
        release.SetResult();

        Assert.False(second);
        Assert.True(await first);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AfterCompletion_NextCallGoesThrough()
    {
        var gate = new NavigationGate();
        var calls = 0;

        Assert.True(await gate.TryRunAsync(() => { calls++; return Task.CompletedTask; }));
        Assert.True(await gate.TryRunAsync(() => { calls++; return Task.CompletedTask; }));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExceptionInNavigation_ReleasesTheGate()
    {
        var gate = new NavigationGate();

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.TryRunAsync(() => throw new InvalidOperationException("boom")));

        Assert.True(await gate.TryRunAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task WithCooldown_APushRightAfterTheFirst_IsIgnored_ALaterOneRuns()
    {
        // Cooldown 600 ms: a second push 100 ms after the first finished is a double tap; one 700 ms later is a new tap.
        var time = new ManualTime();
        var gate = new NavigationGate(time, TimeSpan.FromMilliseconds(600));
        var calls = 0;

        Assert.True(await gate.TryRunAsync(() => { calls++; return Task.CompletedTask; }));
        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.False(await gate.TryRunAsync(() => { calls++; return Task.CompletedTask; }));
        time.Advance(TimeSpan.FromMilliseconds(600));
        Assert.True(await gate.TryRunAsync(() => { calls++; return Task.CompletedTask; }));

        Assert.Equal(2, calls);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks = 1_000_000;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
