using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Base class of all view models. Receives <see cref="LanguageChangedMessage"/> through the weak messenger,
/// so a view model never needs to unsubscribe and is never kept alive by a service.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, IRecipient<LanguageChangedMessage>
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// True while the first load of the screen takes longer than <see cref="LoadingHintDelay"/> (R-52, UI_GUIDELINES §10):
    /// the page then shows the muted "Loading…" text in place of the content. Only for screens that override
    /// <see cref="LoadingHintApplies"/>.
    /// </summary>
    [ObservableProperty]
    private bool _showLoading;

    /// <summary>A load faster than this shows no "Loading…" text at all.</summary>
    public static TimeSpan LoadingHintDelay { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>True while the screen has nothing to show yet (usually <c>!IsLoaded</c>); false = no loading hint.</summary>
    protected virtual bool LoadingHintApplies => false;

    protected ViewModelBase()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <summary>
    /// Where <see cref="RunSafeAsync"/> sends unexpected exceptions; set once at app startup. When null (unit tests)
    /// exceptions are rethrown, so a test notices a handler that fails.
    /// </summary>
    public static IErrorReporter? ErrorReporter { get; set; }

    /// <summary>
    /// Source of the data version for <see cref="ReloadGate"/>; set once at app startup together with <see cref="GateClock"/>.
    /// When null (most unit tests) nothing is skipped and every appearance reloads.
    /// </summary>
    public static IDataChangeNotifier? DataChanges { get; set; }

    /// <summary>The clock of the reload gates (a new day reloads).</summary>
    public static IClock? GateClock { get; set; }

    private ReloadGate? _reloadGate;

    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    private CancellationTokenSource? _loadCts;

    /// <summary>Leaving the page cancels the load still running for it, so it never touches the UI afterwards.</summary>
    public virtual Task OnDisappearingAsync()
    {
        CancelLoad();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts a new load: the previous one (if still running) is cancelled — latest wins — and the returned token is
    /// cancelled when a newer load starts or the page disappears.
    /// </summary>
    protected CancellationToken BeginLoad()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _loadCts, next);
        previous?.Cancel();
        previous?.Dispose();
        return next.Token;
    }

    private async Task ShowLoadingHintAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(LoadingHintDelay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested && LoadingHintApplies)
        {
            ShowLoading = true;
        }
    }

    /// <summary>Cancels the load in progress, if any.</summary>
    protected void CancelLoad()
    {
        var current = Interlocked.Exchange(ref _loadCts, null);
        current?.Cancel();
        current?.Dispose();
    }

    /// <summary>
    /// Runs a page load as the latest one (older loads are cancelled), measures it and logs it when slow
    /// (<see cref="PerformanceLog"/>). A cancelled load ends silently. <paramref name="countItems"/> tells how many
    /// items the page shows afterwards.
    /// </summary>
    protected async Task RunLoadAsync(Func<CancellationToken, Task> load, Func<int>? countItems = null)
    {
        ArgumentNullException.ThrowIfNull(load);

        var token = BeginLoad();
        var watch = PerformanceLog.Start();

        // The hint has its own token, cancelled when this load ends in any way: a load that fails before the hint delay
        // must not leave "Loading…" on the page (T-036a check C-03).
        using var hint = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (LoadingHintApplies)
        {
            _ = ShowLoadingHintAsync(hint.Token);
        }

        try
        {
            await load(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            hint.Cancel();
            ShowLoading = false;
        }

        if (!token.IsCancellationRequested)
        {
            PerformanceLog.Report(GetType().Name, watch.Elapsed, countItems?.Invoke() ?? 0);
        }
    }

    /// <summary>
    /// Like <see cref="RunLoadAsync"/>, but skipped when nothing was saved since this view model last loaded, the day and the
    /// language are the same and <paramref name="hasData"/> says it still shows data (T-028i). The skip is logged in Debug.
    /// </summary>
    protected async Task RunGatedLoadAsync(Func<CancellationToken, Task> load, Func<int>? countItems = null, bool hasData = true)
    {
        ArgumentNullException.ThrowIfNull(load);

        var gate = _reloadGate ??= DataChanges is { } changes && GateClock is { } clock ? new ReloadGate(changes, clock) : null;
        if (gate is { IsFresh: true } && hasData)
        {
            PerformanceLog.ReportSkipped(GetType().Name);
            return;
        }

        // Read before the data is read: a save during the load makes the load stale.
        var snapshot = gate?.Snapshot();
        var completed = false;
        await RunLoadAsync(
            async ct =>
            {
                await load(ct);
                completed = !ct.IsCancellationRequested;
            },
            countItems);
        if (completed && snapshot is { } taken)
        {
            gate?.MarkLoaded(taken);
        }
    }

    /// <summary>The next appearance reloads even if nothing was saved (e.g. a filter or the data source changed).</summary>
    protected void InvalidateReloadGate() => _reloadGate?.Invalidate();

    /// <summary>Runs database / calculation work on a pool thread; the caller assigns the result on the UI thread afterwards.</summary>
    protected static Task<T> OffUiAsync<T>(Func<Task<T>> work, CancellationToken ct) => Task.Run(work, ct);

    /// <summary>
    /// Runs store / database work of an editor (load, save, delete) on a pool thread: Microsoft.Data.Sqlite has no real
    /// asynchronous I/O, so awaiting a store on the UI thread blocks it. Properties are set after the await, on the UI thread.
    /// </summary>
    protected static Task<T> OffUiAsync<T>(Func<Task<T>> work) => Task.Run(work);

    /// <summary>Like <see cref="OffUiAsync{T}(Func{Task{T}})"/> for work without a result.</summary>
    protected static Task OffUiAsync(Func<Task> work) => Task.Run(work);

    /// <summary>Re-reads all localized texts; called after the language changed.</summary>
    protected virtual void RefreshTexts()
    {
    }

    void IRecipient<LanguageChangedMessage>.Receive(LanguageChangedMessage message)
    {
        InvalidateReloadGate();
        RefreshTexts();
    }

    /// <summary>
    /// Runs a command handler so that an unexpected exception is logged and shown with the standard error dialog
    /// instead of crashing the app. Every async command handler goes through here.
    /// </summary>
    protected async Task RunSafeAsync(Func<Task> action, [System.Runtime.CompilerServices.CallerMemberName] string source = "")
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ErrorReporter is not { } reporter)
            {
                throw;
            }

            await reporter.ReportAsync($"{GetType().Name}.{source}", ex);
        }
    }
}
