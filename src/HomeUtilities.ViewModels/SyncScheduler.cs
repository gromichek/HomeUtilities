using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Decides when the sync runs (T-031; nothing runs in the background on Android): shortly after the app start, when it is
/// resumed, about ten seconds after the last saved change, and at once when the app is left with a change still waiting.
/// "Sync now" is a command of the Sync page, not of this class. A run that fails only changes the status line.
/// </summary>
public sealed class SyncScheduler : IDisposable
{
    /// <summary>Quiet time after the last saved change before the upload starts.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ResumeThrottle = TimeSpan.FromSeconds(30);

    private readonly ISyncEngine _engine;
    private readonly IDataChangeNotifier _changes;
    private readonly IClock _clock;
    private readonly IErrorLog? _errorLog;
    private readonly object _lock = new();
    private CancellationTokenSource? _debounce;
    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;
    private readonly TimeSpan _delay;
    private bool _pending;

    public SyncScheduler(ISyncEngine engine, IDataChangeNotifier changes, IClock clock, IErrorLog? errorLog = null, TimeSpan? debounce = null)
    {
        _delay = debounce ?? DebounceDelay;
        _engine = engine;
        _changes = changes;
        _clock = clock;
        _errorLog = errorLog;
        _changes.Changed += OnChanged;
    }

    /// <summary>Reads the stored state and, when sync is on, syncs once (call after the first screen is shown).</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        try
        {
            await _engine.InitializeAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _errorLog?.Append("Sync start", ex);
            return;
        }

        await RunAsync(SyncTrigger.Start, ct);
    }

    /// <summary>The app came back to the foreground.</summary>
    public Task ResumeAsync(CancellationToken ct = default) =>
        _clock.UtcNow - _lastRun < ResumeThrottle ? Task.CompletedTask : RunAsync(SyncTrigger.Resume, ct);

    /// <summary>The app is left: a change that still waits for the debounce is uploaded at once.</summary>
    public Task FlushAsync(CancellationToken ct = default)
    {
        bool pending;
        lock (_lock)
        {
            pending = _pending;
            _debounce?.Cancel();
            _debounce = null;
        }

        return pending ? RunAsync(SyncTrigger.Leaving, ct) : Task.CompletedTask;
    }

    public void Dispose()
    {
        _changes.Changed -= OnChanged;
        lock (_lock)
        {
            _debounce?.Cancel();
        }
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        // The rows a sync applies raise the same notification; they are no reason for another run.
        if (!_engine.IsEnabled || _engine.Status.Kind == SyncStateKind.Syncing)
        {
            return;
        }

        CancellationTokenSource cts;
        lock (_lock)
        {
            _pending = true;
            _debounce?.Cancel();
            cts = _debounce = new CancellationTokenSource();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_delay, cts.Token);
                await RunAsync(SyncTrigger.Debounced, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private async Task RunAsync(SyncTrigger trigger, CancellationToken ct)
    {
        try
        {
            lock (_lock)
            {
                _pending = false;
            }

            _lastRun = _clock.UtcNow;
            await _engine.SyncAsync(trigger, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errorLog?.Append("Sync", ex);
        }
    }
}
