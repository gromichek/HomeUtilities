using HomeUtilities.ViewModels;

namespace HomeUtilities.ViewModels.Tests;

public class SnackbarServiceTests
{
    /// <summary>A delay the test completes by hand; remembers the requested durations.</summary>
    private sealed class ManualDelay
    {
        private readonly List<(TimeSpan Duration, TaskCompletionSource Done)> _waits = [];

        public IReadOnlyList<TimeSpan> Durations => [.. _waits.Select(w => w.Duration)];

        public Task Wait(TimeSpan duration, CancellationToken ct)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => done.TrySetCanceled(ct));
            _waits.Add((duration, done));
            return done.Task;
        }

        /// <summary>The time of the last wait elapses.</summary>
        public void ElapseLast() => _waits[^1].Done.TrySetResult();
    }

    [Fact]
    public async Task PlainMessage_IsVisibleThreeSeconds_ThenDisappearsWithFalse()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);

        var shown = snackbar.ShowAsync("Saved");

        Assert.True(snackbar.IsVisible);
        Assert.Equal("Saved", snackbar.Message);
        Assert.False(snackbar.HasAction);
        Assert.Equal([TimeSpan.FromSeconds(3)], delay.Durations);
        Assert.False(shown.IsCompleted);

        delay.ElapseLast();

        Assert.False(await shown);
        Assert.False(snackbar.IsVisible);
    }

    [Fact]
    public async Task MessageWithAction_StaysSixSeconds()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);

        var shown = snackbar.ShowAsync("Bill marked as paid", "Undo");

        Assert.True(snackbar.HasAction);
        Assert.Equal("Undo", snackbar.ActionText);
        Assert.Equal([TimeSpan.FromSeconds(6)], delay.Durations);

        delay.ElapseLast();
        Assert.False(await shown);
    }

    [Fact]
    public async Task ExplicitDuration_IsUsed()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);

        _ = snackbar.ShowAsync("Long", "Undo", TimeSpan.FromSeconds(10));

        Assert.Equal([TimeSpan.FromSeconds(10)], delay.Durations);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TappingTheAction_CompletesWithTrue_AndHidesTheBar()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);
        var shown = snackbar.ShowAsync("Bill marked as paid", "Undo");

        snackbar.InvokeActionCommand.Execute(null);

        Assert.True(await shown);
        Assert.False(snackbar.IsVisible);
    }

    [Fact]
    public async Task ActionAfterTimeout_ChangesNothing()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);
        var shown = snackbar.ShowAsync("Bill marked as paid", "Undo");
        delay.ElapseLast();
        Assert.False(await shown);

        snackbar.InvokeActionCommand.Execute(null);

        Assert.False(snackbar.IsVisible);
    }

    [Fact]
    public async Task NewMessage_ReplacesTheCurrentOne_ReplacedCallReturnsFalse()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);
        var first = snackbar.ShowAsync("Bill marked as paid", "Undo");

        var second = snackbar.ShowAsync("Saved");

        Assert.False(await first);
        Assert.Equal("Saved", snackbar.Message);
        Assert.False(snackbar.HasAction);
        Assert.True(snackbar.IsVisible);
        Assert.False(second.IsCompleted);

        // The action of the replaced message is gone: tapping it does nothing to the plain message.
        snackbar.InvokeActionCommand.Execute(null);
        Assert.True(snackbar.IsVisible);
        Assert.False(second.IsCompleted);
    }

    [Fact]
    public async Task TimerOfAReplacedMessage_DoesNotHideTheNewOne()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);
        var first = snackbar.ShowAsync("One");
        var second = snackbar.ShowAsync("Two");
        Assert.False(await first);

        Assert.True(snackbar.IsVisible);
        Assert.Equal("Two", snackbar.Message);
        delay.ElapseLast();
        Assert.False(await second);
        Assert.False(snackbar.IsVisible);
    }

    [Fact]
    public async Task Dismiss_ClosesEarly_WithFalse()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);
        var shown = snackbar.ShowAsync("Saved");

        snackbar.DismissCommand.Execute(null);

        Assert.False(await shown);
        Assert.False(snackbar.IsVisible);
    }

    [Fact]
    public async Task BlankMessage_IsIgnored()
    {
        var snackbar = new SnackbarService(new ManualDelay().Wait);

        Assert.False(await snackbar.ShowAsync("  "));
        Assert.False(snackbar.IsVisible);
    }

    [Fact]
    public async Task Notify_DoesNotWaitForTheMessageToDisappear()
    {
        var delay = new ManualDelay();
        var snackbar = new SnackbarService(delay.Wait);

        snackbar.Notify("Copied");

        Assert.True(snackbar.IsVisible);
        Assert.Equal("Copied", snackbar.Message);
        await Task.CompletedTask;
    }
}
