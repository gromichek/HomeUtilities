namespace HomeUtilities.ViewModels;

/// <summary>
/// Lets only one navigation run at a time: a second push issued while the first is still pending (quick double tap)
/// is ignored instead of opening the same page twice. With a <see cref="Cooldown"/>, a push that comes right after the
/// previous one finished is ignored too: since editors load off the UI thread (T-039) the push finishes before the page
/// animation, and the second tap of a double tap reached the list again (owner, 2026-11-07: two editors on Android).
/// </summary>
public sealed class NavigationGate(TimeProvider? time = null, TimeSpan? cooldown = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private int _busy;
    private long _finishedAt = long.MinValue;

    /// <summary>How long after a navigation the next one is ignored (zero = only while one is pending).</summary>
    public TimeSpan Cooldown { get; } = cooldown ?? TimeSpan.Zero;

    /// <summary>Runs <paramref name="navigate"/> unless another navigation is pending or has just finished; the gate is released afterwards, also on failure.</summary>
    /// <returns><c>true</c> if the navigation ran, <c>false</c> if it was ignored.</returns>
    public async Task<bool> TryRunAsync(Func<Task> navigate)
    {
        ArgumentNullException.ThrowIfNull(navigate);

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return false;
        }

        var finished = Interlocked.Read(ref _finishedAt);
        if (Cooldown > TimeSpan.Zero && finished != long.MinValue && _time.GetElapsedTime(finished) < Cooldown)
        {
            Volatile.Write(ref _busy, 0);
            return false;
        }

        try
        {
            await navigate();
            return true;
        }
        finally
        {
            Interlocked.Exchange(ref _finishedAt, _time.GetTimestamp());
            Volatile.Write(ref _busy, 0);
        }
    }
}
