using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-039: editors read and write the stores on a pool thread, never on the UI thread; slow opens and saves are logged.</summary>
public class EditorThreadingTests : IDisposable
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeErrorLog _log = new();

    public EditorThreadingTests() => PerformanceLog.Sink = _log;

    public void Dispose() => PerformanceLog.Sink = null;

    [Fact]
    public async Task OpenSaveAndDelete_NeverCallTheStoreOnTheUiThread()
    {
        using var ui = new SingleThreadContext();
        var store = new ThreadRecordingCategoryStore();
        var own = new Category { Name = "Kindergarten" };
        store.Stored.Add(own);

        await ui.RunAsync(async () =>
        {
            var vm = new CategoryEditViewModel(store, new FakeLocalizer(_language), new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService());
            await vm.LoadAsync(own.Id);
            vm.Name = "Kindergarten 2";
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.DeleteCommand.ExecuteAsync(null);

            // The properties were set back on the UI thread.
            Assert.Equal(ui.ThreadId, Environment.CurrentManagedThreadId);
        });

        Assert.NotEmpty(store.CallThreads);
        Assert.DoesNotContain(ui.ThreadId, store.CallThreads);
    }

    [Fact]
    public async Task SlowSave_IsLoggedAsEditorSaveLine()
    {
        var store = new SlowCategoryStore(TimeSpan.FromMilliseconds(350));
        var vm = new CategoryEditViewModel(store, new FakeLocalizer(_language), new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService());
        await vm.LoadAsync(null);
        vm.Name = "Slow";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains(_log.Entries, e => e.Exception.ToString().StartsWith("Performance: CategoryEditViewModel save ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SlowOpen_IsLoggedAsEditorOpenLine()
    {
        var store = new SlowCategoryStore(TimeSpan.FromMilliseconds(350));
        var vm = new CategoryEditViewModel(store, new FakeLocalizer(_language), new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService());

        await vm.LoadAsync(null);

        Assert.Contains(_log.Entries, e => e.Exception.ToString().StartsWith("Performance: CategoryEditViewModel open ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EditorIsNotReadyWhileLoading_AndReadyAfterwards()
    {
        // The page hides the form while IsEditorReady is false, so the fields never pop in one by one.
        var vm = new CategoryEditViewModel(new FakeCategoryStore(), new FakeLocalizer(_language), new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService());
        var seen = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsEditorReady))
            {
                seen.Add(vm.IsEditorReady);
            }
        };

        Assert.True(vm.IsEditorReady);
        await vm.LoadAsync(null);

        Assert.Equal([false, true], seen);
        Assert.True(vm.IsEditorReady);
    }

    [Fact]
    public async Task FastOpenAndSave_LogNothing()
    {
        var vm = new CategoryEditViewModel(new FakeCategoryStore(), new FakeLocalizer(_language), new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService());
        await vm.LoadAsync(null);
        vm.Name = "Fast";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void FormatEditor_IsTheLineTheOwnerCopies() =>
        Assert.Equal("Performance: BillEditViewModel save 412 ms", PerformanceLog.FormatEditor("BillEditViewModel", "save", TimeSpan.FromMilliseconds(412.9)));

    [Fact]
    public async Task SaveCommand_IsDisabledWhileSaving()
    {
        var gate = new TaskCompletionSource();
        var store = new GatedCategoryStore(gate.Task);
        var vm = new CategoryEditViewModel(store, new FakeLocalizer(_language), new FakeDialogService(), new FakeNavigationService(), new FakeSnackbarService());
        await vm.LoadAsync(null);
        vm.Name = "Once";

        var first = vm.SaveCommand.ExecuteAsync(null);
        await TestWait.UntilAsync(() => store.SaveStarted);
        Assert.False(vm.SaveCommand.CanExecute(null));

        gate.SetResult();
        await first;
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(1, store.SaveCalls);
    }

    private sealed class ThreadRecordingCategoryStore : FakeCategoryStoreBase
    {
        public List<int> CallThreads { get; } = [];

        protected override void OnCall() { lock (CallThreads) { CallThreads.Add(Environment.CurrentManagedThreadId); } }
    }

    private sealed class SlowCategoryStore(TimeSpan delay) : FakeCategoryStoreBase
    {
        protected override void OnCall() => Thread.Sleep(delay);
    }

    private sealed class GatedCategoryStore(Task gate) : FakeCategoryStoreBase
    {
        public bool SaveStarted { get; private set; }

        public int SaveCalls { get; private set; }

        public override async Task SaveAsync(Category aggregate, CancellationToken ct = default)
        {
            SaveStarted = true;
            SaveCalls++;
            await gate;
            await base.SaveAsync(aggregate, ct);
        }
    }

    /// <summary>A category store that tells the subclass about every call.</summary>
    private abstract class FakeCategoryStoreBase : HomeUtilities.Core.Abstractions.ICategoryStore
    {
        private readonly FakeCategoryStore _inner = new();

        public List<Category> Stored => _inner.Stored;

        protected virtual void OnCall()
        {
        }

        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default)
        {
            OnCall();
            return _inner.ListAsync(ct);
        }

        public Task<Category?> GetAsync(Guid id, CancellationToken ct = default)
        {
            OnCall();
            return _inner.GetAsync(id, ct);
        }

        public virtual Task SaveAsync(Category aggregate, CancellationToken ct = default)
        {
            OnCall();
            return _inner.SaveAsync(aggregate, ct);
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            OnCall();
            return _inner.DeleteAsync(id, ct);
        }
    }

    /// <summary>A dedicated thread with a synchronization context, standing in for the UI thread.</summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = [];
        private readonly Thread _thread;

        public SingleThreadContext()
        {
            _thread = new Thread(Pump) { IsBackground = true };
            _thread.Start();
            ThreadId = _thread.ManagedThreadId;
        }

        public int ThreadId { get; }

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public Task RunAsync(Func<Task> body)
        {
            var done = new TaskCompletionSource();
            Post(
                _ =>
                {
                    var task = body();
                    task.ContinueWith(t => { if (t.IsFaulted) { done.SetException(t.Exception!.InnerExceptions); } else { done.SetResult(); } }, TaskScheduler.Default);
                },
                null);
            return done.Task;
        }

        public void Dispose() => _queue.CompleteAdding();

        private void Pump()
        {
            SetSynchronizationContext(this);
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }
    }
}
