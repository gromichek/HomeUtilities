using HomeUtilities.Core.Sync;

namespace HomeUtilities.App.Services;

/// <summary>Runs actions on the MAUI main thread (C11): the sync engine raises its events on a worker thread.</summary>
public sealed class MauiUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}
