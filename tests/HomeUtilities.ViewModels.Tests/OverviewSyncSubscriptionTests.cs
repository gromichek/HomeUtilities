using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-041 (M-1): the Overview does not subscribe to the sync engine, so the singleton engine cannot keep it alive.</summary>
public class OverviewSyncSubscriptionTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private sealed class RaisingEngine : ISyncEngine
    {
        public SyncStatus Status { get; set; } = SyncStatus.Off;

        public bool IsEnabled { get; set; }

        public bool NeedsRejoin => false;

        public IReadOnlyList<SyncDetail> Details => [];

        public event EventHandler? StatusChanged;

        public void Raise() => StatusChanged?.Invoke(this, EventArgs.Empty);

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<FirstJoinInfo> InspectAsync(CancellationToken ct = default) => Task.FromResult(new FirstJoinInfo(false, false));

        public Task EnableAsync(FirstJoinChoice choice, CancellationToken ct = default) => Task.CompletedTask;

        public Task DisableAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task ForgetJoinAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task MarkNeedsRejoinAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<CloudFilesReport> ListCloudFilesAsync(CancellationToken ct = default) => Task.FromResult(new CloudFilesReport([], [], 0, 0, 0, 0));

        public Task<bool> SyncAsync(SyncTrigger trigger, CancellationToken ct = default) => Task.FromResult(IsEnabled);
    }

    private sealed class Initializer : IDatabaseInitializer
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyLoader : IOverviewDataLoader
    {
        public Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default) =>
            Task.FromResult(new OverviewData([], [], [], [], [], [], new Dictionary<Guid, decimal>(), []));
    }

    private static OverviewViewModel Create(BillTestKit kit, ISyncEngine engine, IOverviewDataLoader? loader = null) => new(
        kit.Kit.Localizer, kit.Kit.Clock, new Initializer(), loader ?? new EmptyLoader(), kit.Templates, kit.QuickPay, kit.Kit.Toast,
        kit.Kit.Navigation, kit.Kit.Dialogs, kit.Kit.Settings, kit.Kit.Layout, sync: engine);

    private sealed class CountingLoader : IOverviewDataLoader
    {
        public int Loads { get; private set; }

        public Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default)
        {
            Loads++;
            return new EmptyLoader().LoadAsync(today, ct);
        }
    }

    [Fact]
    public async Task Shown_Overview_Reloads_OnceAfterASyncThatReceivedRows()
    {
        var kit = new BillTestKit(Today);
        var engine = new RaisingEngine { IsEnabled = true };
        var loader = new CountingLoader();
        var vm = Create(kit, engine, loader);
        await vm.OnAppearingAsync();
        Assert.Equal(1, loader.Loads);

        // A run that only sent changes: nothing to reload.
        engine.Status = new SyncStatus(SyncStateKind.Synced, LastSyncAt: kit.Kit.Clock.UtcNow, Sent: 2);
        WeakReferenceMessenger.Default.Send(new SyncStatusChangedMessage());
        Assert.Equal(1, loader.Loads);

        // A run that wrote 3 rows of another device: reloaded in place, and only once for that run.
        engine.Status = new SyncStatus(SyncStateKind.Synced, LastSyncAt: kit.Kit.Clock.UtcNow.AddMinutes(1), Received: 3);
        WeakReferenceMessenger.Default.Send(new SyncStatusChangedMessage());
        await WaitUntilAsync(() => loader.Loads == 2);
        WeakReferenceMessenger.Default.Send(new SyncStatusChangedMessage());
        Assert.Equal(2, loader.Loads);

        // Hidden: no reload from the message (the next appearance decides).
        await vm.OnDisappearingAsync();
        engine.Status = new SyncStatus(SyncStateKind.Synced, LastSyncAt: kit.Kit.Clock.UtcNow.AddMinutes(2), Received: 1);
        WeakReferenceMessenger.Default.Send(new SyncStatusChangedMessage());
        Assert.Equal(2, loader.Loads);
        GC.KeepAlive(vm);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Hint_FollowsTheStatusMessage_AndIsCaughtUpWhenAppearing()
    {
        var kit = new BillTestKit(Today);
        var engine = new RaisingEngine { IsEnabled = true };
        var vm = Create(kit, engine);
        await vm.OnAppearingAsync();
        Assert.False(vm.HasSyncHint);

        // Failed for two days: the hint is shown after the notifier's message.
        engine.Status = new SyncStatus(SyncStateKind.Failed, LastSyncAt: kit.Kit.Clock.UtcNow.AddDays(-2));
        WeakReferenceMessenger.Default.Send(new SyncStatusChangedMessage());
        Assert.True(vm.HasSyncHint);

        // Recovered while the page was hidden: no message needed, appearing catches up.
        engine.Status = new SyncStatus(SyncStateKind.Synced, LastSyncAt: kit.Kit.Clock.UtcNow);
        await vm.OnAppearingAsync();
        Assert.False(vm.HasSyncHint);
    }

    [Fact]
    public async Task SyncNotifier_SendsTheMessage_WhenTheEngineStatusChanges()
    {
        var kit = new BillTestKit(Today);
        var engine = new RaisingEngine { IsEnabled = true };
        var vm = Create(kit, engine);
        await vm.OnAppearingAsync();
        using var notifier = new SyncNotifier(engine, kit.Kit.Toast, kit.Kit.Localizer, new SyncSettings(kit.Kit.Settings));

        engine.Status = new SyncStatus(SyncStateKind.WaitingForNetwork, LastSyncAt: kit.Kit.Clock.UtcNow.AddDays(-3));
        engine.Raise();

        Assert.True(vm.HasSyncHint);
        GC.KeepAlive(vm);
    }

    [Fact]
    public async Task Overview_IsCollected_WhileTheSyncEngineLives()
    {
        var engine = new RaisingEngine { IsEnabled = true };
        var weak = await CreateShowAndHideAsync(engine);

        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive, "The singleton engine must not keep the view model alive.");
        GC.KeepAlive(engine);
    }

    // Separate method so no local of the test keeps a strong reference.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CreateShowAndHideAsync(RaisingEngine engine)
    {
        var vm = Create(new BillTestKit(Today), engine);
        await vm.OnAppearingAsync();
        await vm.OnDisappearingAsync();
        return new WeakReference(vm);
    }
}
