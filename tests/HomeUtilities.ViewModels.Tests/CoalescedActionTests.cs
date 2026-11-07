using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-039: the batching behind <c>AdaptiveFieldGrid</c> — one arrange per batch of children.</summary>
public class CoalescedActionTests
{
    [Fact]
    public void TwentyRequestsInOneTurn_RunTheActionOnce()
    {
        var queue = new Queue<Action>();
        var work = new CoalescedAction(() => { }, run => { queue.Enqueue(run); return true; });

        for (var i = 0; i < 20; i++)
        {
            work.Request();
        }

        Assert.Equal(0, work.Runs);
        Assert.True(work.IsPending);
        Assert.Single(queue);

        queue.Dequeue()();

        Assert.Equal(1, work.Runs);
        Assert.False(work.IsPending);
    }

    [Fact]
    public void ARequestAfterTheRun_SchedulesAgain()
    {
        var queue = new Queue<Action>();
        var work = new CoalescedAction(() => { }, run => { queue.Enqueue(run); return true; });

        work.Request();
        queue.Dequeue()();
        work.Request();
        queue.Dequeue()();

        Assert.Equal(2, work.Runs);
    }

    [Fact]
    public void WithoutADispatcher_TheActionRunsAtOnce()
    {
        var work = new CoalescedAction(() => { }, _ => false);

        work.Request();
        work.Request();

        Assert.Equal(2, work.Runs);
    }

    [Fact]
    public void RunNow_CancelsTheWaitingRequest()
    {
        var queue = new Queue<Action>();
        var work = new CoalescedAction(() => { }, run => { queue.Enqueue(run); return true; });

        work.Request();
        work.RunNow();
        queue.Dequeue()();

        Assert.Equal(1, work.Runs);
        Assert.False(work.IsPending);
    }
}
