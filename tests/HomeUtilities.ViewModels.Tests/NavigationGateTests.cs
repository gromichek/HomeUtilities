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
}
