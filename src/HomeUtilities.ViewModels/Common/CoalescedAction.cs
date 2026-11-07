namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Runs an action once for a whole batch of requests (T-039): the first <see cref="Request"/> schedules the action with the
/// <c>post</c> delegate (the dispatcher of the UI thread), further requests before it ran are absorbed. A page that adds 20
/// children one after the other thus lays them out once instead of 20 times.
/// </summary>
public sealed class CoalescedAction
{
    private readonly Action _action;
    private readonly Func<Action, bool> _post;
    private int _pending;

    /// <param name="action">The work to do once per batch.</param>
    /// <param name="post">
    /// Schedules a callback for a later dispatcher turn; returns false when that is not possible (no dispatcher yet), then the
    /// action runs at once.
    /// </param>
    public CoalescedAction(Action action, Func<Action, bool> post)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(post);
        _action = action;
        _post = post;
    }

    /// <summary>How many times the action ran.</summary>
    public int Runs { get; private set; }

    /// <summary>True between a request and the run of the action.</summary>
    public bool IsPending => Volatile.Read(ref _pending) == 1;

    /// <summary>Asks for the action; it runs once on a later dispatcher turn, however many requests came before.</summary>
    public void Request()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        if (!_post(RunIfPending))
        {
            RunIfPending();
        }
    }

    /// <summary>Runs the action now (a size change, a changed setting) and cancels the request still waiting.</summary>
    public void RunNow()
    {
        Volatile.Write(ref _pending, 0);
        Run();
    }

    private void RunIfPending()
    {
        if (Interlocked.Exchange(ref _pending, 0) == 1)
        {
            Run();
        }
    }

    private void Run()
    {
        Runs++;
        _action();
    }
}
