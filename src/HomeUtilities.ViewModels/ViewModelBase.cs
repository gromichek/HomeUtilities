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

    protected ViewModelBase()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <summary>
    /// Where <see cref="RunSafeAsync"/> sends unexpected exceptions; set once at app startup. When null (unit tests)
    /// exceptions are rethrown, so a test notices a handler that fails.
    /// </summary>
    public static IErrorReporter? ErrorReporter { get; set; }

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
        try
        {
            await load(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested)
        {
            PerformanceLog.Report(GetType().Name, watch.Elapsed, countItems?.Invoke() ?? 0);
        }
    }

    /// <summary>Runs database / calculation work on a pool thread; the caller assigns the result on the UI thread afterwards.</summary>
    protected static Task<T> OffUiAsync<T>(Func<Task<T>> work, CancellationToken ct) => Task.Run(work, ct);

    /// <summary>Re-reads all localized texts; called after the language changed.</summary>
    protected virtual void RefreshTexts()
    {
    }

    void IRecipient<LanguageChangedMessage>.Receive(LanguageChangedMessage message) => RefreshTexts();

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
