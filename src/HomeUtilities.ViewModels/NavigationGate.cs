namespace HomeUtilities.ViewModels;

/// <summary>
/// Lets only one navigation run at a time: a second push issued while the first is still pending (quick double tap)
/// is ignored instead of opening the same page twice.
/// </summary>
public sealed class NavigationGate
{
    private int _busy;

    /// <summary>Runs <paramref name="navigate"/> unless another navigation is pending; the gate is released afterwards, also on failure.</summary>
    /// <returns><c>true</c> if the navigation ran, <c>false</c> if it was ignored.</returns>
    public async Task<bool> TryRunAsync(Func<Task> navigate)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            await navigate();
            return true;
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }
}
