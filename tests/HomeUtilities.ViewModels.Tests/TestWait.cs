namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// Waits for work that a command started and did not return a task for (T-039: editors read and write the stores on a pool
/// thread, so a fire-and-forget command finishes a moment after <c>Execute</c> returns).
/// </summary>
internal static class TestWait
{
    public static async Task UntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }
}
