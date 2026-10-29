using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Passes the data of a tapped notification to <see cref="NotificationTapHandler"/>. A tap that starts the app arrives
/// before the Shell exists: it is kept until the window is ready (<see cref="Attach"/>).
/// </summary>
public sealed class NotificationTapRouter
{
    private readonly object _lock = new();
    private Func<string?, Task>? _open;
    private bool _hasPending;
    private string? _pending;

    /// <summary>A notification was tapped; opens its target now or as soon as the app is ready.</summary>
    public void Submit(string? data)
    {
        Func<string?, Task>? open;
        lock (_lock)
        {
            open = _open;
            if (open is null)
            {
                _hasPending = true;
                _pending = data;
                return;
            }
        }

        Run(open, data);
    }

    /// <summary>The window is ready: opens a tap that arrived earlier and handles later ones at once.</summary>
    public void Attach(Func<string?, Task> open)
    {
        string? data = null;
        bool pending;
        lock (_lock)
        {
            _open = open;
            pending = _hasPending;
            if (pending)
            {
                data = _pending;
                _hasPending = false;
                _pending = null;
            }
        }

        if (pending)
        {
            Run(open, data);
        }
    }

    private static void Run(Func<string?, Task> open, string? data) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await open(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Opening the notification target failed: {ex}");
            }
        });
}
