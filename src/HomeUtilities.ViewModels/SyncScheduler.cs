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

    /// <summary>How often a failed or waiting sync is tried again while the app is in the foreground (C10).</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ResumeThrottle = TimeSpan.FromSeconds(30);

    private readonly ISyncEngine _engine;
    private readonly IDataChangeNotifier _changes;
    private readonly IClock _clock;
    private readonly IErrorLog? _errorLog;
    private readonly object _lock = new();
    private CancellationTokenSource? _debounce;
    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;
    private readonly TimeSpan _delay;
    private readonly ISyncConnectivity? _connectivity;
    private readonly TimeSpan _retry;
    private Timer? _retryTimer;
    private bool _foreground;
    private bool _pending;

    public SyncScheduler(
        ISyncEngine engine,
        IDataChangeNotifier changes,
        IClock clock,
        IErrorLog? errorLog = null,
        TimeSpan? debounce = null,
        ISyncConnectivity? connectivity = null,
        TimeSpan? retryInterval = null)
    {
        _delay = debounce ?? DebounceDelay;
        _retry = retryInterval ?? RetryInterval;
        _connectivity = connectivity;
        if (connectivity is not null)
        {
            connectivity.Changed += OnConnectivityChanged;
        }

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

        SetForeground(true);
        await RunAsync(SyncTrigger.Start, ct);
    }

    /// <summary>The window became active again (it may not have been stopped, e.g. focus change on Windows): the retry timer runs.</summary>
    public void NotifyForeground() => SetForeground(true);

    /// <summary>True while the retry timer runs (the app is in the foreground).</summary>
    public bool IsForeground
    {
        get
        {
            lock (_lock)
            {
                return _foreground;
            }
        }
    }

    // The retry timer exists only in the foreground: nothing runs in the background (battery rule, owner 2026-11-03).
    private void SetForeground(bool value)
    {
        lock (_lock)
        {
            _foreground = value;
            if (value)
            {
                _retryTimer ??= new Timer(_ => _ = RetryTickAsync(), null, _retry, _retry);
            }
            else
            {
                _retryTimer?.Dispose();
                _retryTimer = null;
            }
        }
    }

    private static bool NeedsRetry(SyncStatus status) =>
        status.Kind is SyncStateKind.WaitingForNetwork or SyncStateKind.Failed;

    /// <summary>One tick of the retry timer: syncs again when the status is "waiting for the network" or "failed". Public for tests.</summary>
    public Task RetryTickAsync()
    {
        if (!IsForeground || !_engine.IsEnabled || !NeedsRetry(_engine.Status) || _connectivity is { IsConnected: false })
        {
            return Task.CompletedTask;
        }

        return RunAsync(SyncTrigger.Retry, CancellationToken.None);
    }

    private void OnConnectivityChanged(object? sender, EventArgs e)
    {
        if (_connectivity is { IsConnected: true } && IsForeground && _engine.IsEnabled && _engine.Status.Kind != SyncStateKind.Syncing)
        {
            _ = Task.Run(() => RunAsync(SyncTrigger.Network, CancellationToken.None));
        }
    }

    /// <summary>The app came back to the foreground.</summary>
    public Task ResumeAsync(CancellationToken ct = default)
    {
        SetForeground(true);
        return _clock.UtcNow - _lastRun < ResumeThrottle ? Task.CompletedTask : RunAsync(SyncTrigger.Resume, ct);
    }

    /// <summary>The app is left: a change that still waits for the debounce is uploaded at once.</summary>
    public Task FlushAsync(CancellationToken ct = default)
    {
        SetForeground(false);
        bool pending;
        lock (_lock)
        {
            pending = _pending;
            CancelDebounce();
        }

        return pending ? RunAsync(SyncTrigger.Leaving, ct) : Task.CompletedTask;
    }

    public void Dispose()
    {
        _changes.Changed -= OnChanged;
        if (_connectivity is not null)
        {
            _connectivity.Changed -= OnConnectivityChanged;
        }

        lock (_lock)
        {
            CancelDebounce();
            _retryTimer?.Dispose();
            _retryTimer = null;
        }
    }

    // Cancel first, then dispose: the waiting task only holds the (cancelled) token. Call with _lock held.
    private void CancelDebounce()
    {
        if (_debounce is { } old)
        {
            _debounce = null;
            old.Cancel();
            old.Dispose();
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
        CancellationToken token;
        lock (_lock)
        {
            _pending = true;
            CancelDebounce();
            cts = _debounce = new CancellationTokenSource();
            token = cts.Token; // read now: a later change disposes this source
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_delay, token);
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
