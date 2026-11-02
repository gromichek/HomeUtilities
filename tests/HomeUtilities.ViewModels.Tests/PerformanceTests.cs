using System.Collections.ObjectModel;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-028b: timing log, latest-wins loads, in-place list updates and released view models.</summary>
public class PerformanceTests : IDisposable
{
    private readonly FakeErrorLog _log = new();

    public PerformanceTests() => PerformanceLog.Sink = _log;

    public void Dispose() => PerformanceLog.Sink = null;

    [Fact]
    public void Format_IsTheLineTheOwnerCopies()
    {
        Assert.Equal("Performance: BillsViewModel 412 ms (100 items)", PerformanceLog.Format("BillsViewModel", TimeSpan.FromMilliseconds(412.9), 100));
    }

    [Fact]
    public void Report_Below300Ms_LogsNothing()
    {
        Assert.False(PerformanceLog.Report("X", TimeSpan.FromMilliseconds(299), 5));
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void Report_At300Ms_IsWrittenToTheErrorLog()
    {
        Assert.True(PerformanceLog.Report("X", TimeSpan.FromMilliseconds(300), 5));

        var entry = Assert.Single(_log.Entries);
        Assert.Equal("Performance", entry.Source);
        Assert.Equal("Performance: X 300 ms (5 items)", entry.Exception.ToString());
    }

    [Fact]
    public async Task Load_NewerLoadCancelsTheOlderOne_AndOnlyTheLatestFinishes()
    {
        var vm = new LoadingViewModel();
        var firstStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var first = vm.LoadAsync(async ct =>
        {
            firstStarted.SetResult();
            await release.Task.WaitAsync(ct);
            vm.Applied.Add("first");
        });
        await firstStarted.Task;
        var second = vm.LoadAsync(_ =>
        {
            vm.Applied.Add("second");
            return Task.CompletedTask;
        });
        await Task.WhenAll(first, second);

        Assert.Equal(["second"], vm.Applied);
    }

    [Fact]
    public async Task Load_LeavingThePageCancelsTheRunningLoad()
    {
        var vm = new LoadingViewModel();
        var started = new TaskCompletionSource();

        var load = vm.LoadAsync(async ct =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            vm.Applied.Add("never");
        });
        await started.Task;
        await vm.OnDisappearingAsync();
        await load;

        Assert.Empty(vm.Applied);
        Assert.True(vm.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Load_OfTwentyRapidAppearances_EndsWithTheLastData()
    {
        var store = new FakeSupplierStore();
        store.Stored.Add(new Supplier { Name = "Gas" });
        var vm = CreateSuppliers(store);

        // Twenty menu taps: every appearing starts a load, the page disappears each time before the next one.
        var loads = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            loads.Add(vm.OnAppearingAsync());
            if (i < 19)
            {
                await vm.OnDisappearingAsync();
            }
        }

        await Task.WhenAll(loads);

        Assert.True(vm.IsLoaded);
        Assert.Equal(["Gas"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public void ItemsSync_KeepsTheCollectionInstanceAndOrder()
    {
        var items = new ObservableCollection<Row> { new(1, "a"), new(2, "b"), new(3, "c") };
        var changes = new List<string>();
        items.CollectionChanged += (_, e) => changes.Add(e.Action.ToString());

        ItemsSync.Update(items, [new Row(3, "c2"), new Row(1, "a2"), new Row(4, "d")], r => r.Id);

        Assert.Equal(["c2", "a2", "d"], items.Select(r => r.Name));
        Assert.DoesNotContain("Reset", changes);
    }

    [Fact]
    public void ItemsSync_EmptySourceClears_AndSameKeysAreReplacedInPlace()
    {
        var items = new ObservableCollection<Row> { new(1, "a"), new(2, "b") };

        ItemsSync.Update(items, [new Row(1, "a2"), new Row(2, "b2")], r => r.Id);
        Assert.Equal(["a2", "b2"], items.Select(r => r.Name));

        ItemsSync.Update(items, [], r => r.Id);
        Assert.Empty(items);
    }

    [Fact]
    public async Task ViewModels_AreCollectedAfterTheyAreDropped()
    {
        // Transient view models are created for every menu tap: nothing static (messenger, services) may keep them alive.
        var weak = await CreateAndLoadAsync();

        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(10);
        }

        Assert.False(weak.IsAlive);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private async Task<WeakReference> CreateAndLoadAsync()
    {
        var vm = CreateSuppliers(new FakeSupplierStore());
        await vm.OnAppearingAsync();
        await vm.OnDisappearingAsync();
        return new WeakReference(vm);
    }

    private static SuppliersViewModel CreateSuppliers(FakeSupplierStore store)
    {
        var language = new FakeLanguageService();
        var localizer = new FakeLocalizer(language);
        var navigation = new FakeNavigationService();
        return new SuppliersViewModel(
            store,
            localizer,
            navigation,
            new FakeLayoutModeService(),
            new SectionSelectionMemory(),
            () => new SupplierEditViewModel(
                store, new TestClock(new DateOnly(2026, 10, 20)), localizer, new FakeDialogService(), navigation,
                new FakeLauncherService(), new FakeClipboardService(), new FakeSnackbarService()));
    }

    private sealed record Row(int Id, string Name);
}

internal sealed class LoadingViewModel : ViewModelBase
{
    public List<string> Applied { get; } = [];

    public CancellationToken LastToken { get; private set; }

    public Task LoadAsync(Func<CancellationToken, Task> load) => RunLoadAsync(ct =>
    {
        LastToken = ct;
        return load(ct);
    });
}
