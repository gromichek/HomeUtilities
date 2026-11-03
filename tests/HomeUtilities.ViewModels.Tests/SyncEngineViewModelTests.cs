using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;
using HomeUtilities.ViewModels;

namespace HomeUtilities.ViewModels.Tests;

public class SyncEngineViewModelTests
{
    private sealed class FakeEngine : ISyncEngine
    {
        public SyncStatus Status { get; set; } = SyncStatus.Off;

        public bool IsEnabled { get; set; }

        public FirstJoinInfo Info { get; set; } = new(false, false);

        public Exception? InspectFailure { get; set; }

        public List<FirstJoinChoice> Enabled { get; } = [];

        public List<SyncTrigger> Runs { get; } = [];

        public int DisableCalls { get; private set; }

        public IReadOnlyList<SyncDetail> Details { get; set; } = [];

        public event EventHandler? StatusChanged;

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<FirstJoinInfo> InspectAsync(CancellationToken ct = default) =>
            InspectFailure is null ? Task.FromResult(Info) : Task.FromException<FirstJoinInfo>(InspectFailure);

        public Task EnableAsync(FirstJoinChoice choice, CancellationToken ct = default)
        {
            Enabled.Add(choice);
            IsEnabled = true;
            Status = new SyncStatus(SyncStateKind.Synced, new DateTimeOffset(2026, 11, 2, 16, 42, 0, TimeSpan.Zero));
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task DisableAsync(CancellationToken ct = default)
        {
            DisableCalls++;
            IsEnabled = false;
            Status = SyncStatus.Off;
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task<bool> SyncAsync(SyncTrigger trigger, CancellationToken ct = default)
        {
            Runs.Add(trigger);
            return Task.FromResult(IsEnabled);
        }
    }

    private readonly FakeEngine _engine = new();
    private readonly SyncSettingsViewModelTests.FakeAccount _account = new() { State = SyncAccountState.SignedIn("anna@example.test") };
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAutoBackupService _backups = new();

    private SyncSettingsViewModel Vm() => new(
        _account,
        new NoStorage(),
        new FakeLocalizer(new FakeLanguageService()),
        _dialogs,
        new FakeErrorLog(),
        _engine,
        _backups);

    private sealed class NoStorage : ISyncStorage
    {
        public Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SyncFileInfo>>([]);

        public Task UploadAsync(string path, Stream content, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Stream> DownloadAsync(string path, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task SettleAsync(SyncSettingsViewModel vm)
    {
        for (var i = 0; i < 100 && vm.IsBusy; i++)
        {
            await Task.Delay(10);
        }

        await Task.Delay(30);
    }

    [Fact]
    public async Task Switching_on_with_an_empty_cloud_starts_here_without_a_question()
    {
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.StartHere], _engine.Enabled);
        Assert.True(vm.SyncOn);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task Switching_on_an_empty_device_downloads_without_asking_or_backing_up()
    {
        _engine.Info = new FirstJoinInfo(true, false);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task Replace_is_listed_first_and_makes_a_backup_copy_before_the_join()
    {
        _engine.Info = new FirstJoinInfo(true, true);
        IReadOnlyList<string>? shown = null;
        _dialogs.ChooseScript = options =>
        {
            shown = options;
            return options[0];
        };
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal(["SyncJoinReplace [en]", "SyncJoinMerge [en]"], shown);
        Assert.Equal([AutoBackupReason.BeforeRestore], _backups.Backups);
        Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
    }

    [Fact]
    public async Task Merge_needs_no_backup_and_cancel_leaves_sync_off()
    {
        _engine.Info = new FirstJoinInfo(true, true);
        _dialogs.ChooseScript = options => options[1];
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);
        Assert.Equal([FirstJoinChoice.Merge], _engine.Enabled);
        Assert.Empty(_backups.Backups);

        var second = new FakeEngine { Info = new FirstJoinInfo(true, true) };
        _dialogs.ChooseScript = _ => null;
        var vm2 = new SyncSettingsViewModel(_account, new NoStorage(), new FakeLocalizer(new FakeLanguageService()), _dialogs, new FakeErrorLog(), second, _backups);
        vm2.SyncOn = true;
        await SettleAsync(vm2);
        Assert.Empty(second.Enabled);
        Assert.False(vm2.SyncOn);
    }

    [Fact]
    public async Task A_failed_check_of_the_cloud_shows_the_reason_and_leaves_the_switch_off()
    {
        _engine.InspectFailure = new SyncException(SyncErrorKind.NoNetwork, "offline");
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.False(vm.SyncOn);
        Assert.Equal("SyncError_NoNetwork [en]", vm.MessageText);
        Assert.Empty(_engine.Enabled);
    }

    [Fact]
    public async Task Sync_now_runs_a_manual_sync_and_disconnect_switches_sync_off_first()
    {
        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.Synced, new DateTimeOffset(2026, 11, 2, 16, 42, 0, TimeSpan.Zero));
        var vm = Vm();
        Assert.True(vm.SyncOn);
        Assert.True(vm.SyncNowCommand.CanExecute(null));

        await vm.SyncNowCommand.ExecuteAsync(null);
        Assert.Equal([SyncTrigger.Manual], _engine.Runs);

        await vm.DisconnectCommand.ExecuteAsync(null);
        Assert.Equal(1, _engine.DisableCalls);
        Assert.False(vm.SyncOn);
    }

    [Theory]
    [InlineData(SyncStateKind.WaitingForNetwork, "SyncRunWaiting [en]")]
    [InlineData(SyncStateKind.UpdateRequired, "SyncRunUpdate [en]")]
    [InlineData(SyncStateKind.Syncing, "SyncRunSyncing [en]")]
    public async Task The_status_line_follows_the_engine(SyncStateKind kind, string expected)
    {
        _engine.IsEnabled = true;
        var vm = Vm();
        _engine.Status = new SyncStatus(kind);
        await vm.OnAppearingAsync();

        Assert.Equal(expected, vm.SyncRunText);
    }

    // ---- Scheduler ----

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

        public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    [Fact]
    public async Task A_saved_change_starts_one_debounced_run_and_leaving_the_app_runs_it_at_once()
    {
        _engine.IsEnabled = true;
        var notifier = new DataChangeNotifier();
        using var scheduler = new SyncScheduler(_engine, notifier, new Clock(), debounce: TimeSpan.FromMilliseconds(100));

        notifier.Notify();
        notifier.Notify();
        await Task.Delay(400);
        Assert.Equal([SyncTrigger.Debounced], _engine.Runs);

        _engine.Runs.Clear();
        notifier.Notify();
        await scheduler.FlushAsync();
        Assert.Equal([SyncTrigger.Leaving], _engine.Runs);
        await Task.Delay(300);
        Assert.Equal([SyncTrigger.Leaving], _engine.Runs);

        await scheduler.FlushAsync();
        Assert.Equal([SyncTrigger.Leaving], _engine.Runs);
    }

    [Fact]
    public async Task Changes_while_sync_is_off_or_running_start_nothing_and_resume_is_throttled()
    {
        var notifier = new DataChangeNotifier();
        var clock = new Clock();
        using var scheduler = new SyncScheduler(_engine, notifier, clock, debounce: TimeSpan.FromMilliseconds(50));

        notifier.Notify();
        await Task.Delay(200);
        Assert.Empty(_engine.Runs);

        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.Syncing);
        notifier.Notify();
        await Task.Delay(200);
        Assert.Empty(_engine.Runs);

        _engine.Status = new SyncStatus(SyncStateKind.Synced);
        await scheduler.StartAsync();
        await scheduler.ResumeAsync();
        Assert.Equal([SyncTrigger.Start], _engine.Runs);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await scheduler.ResumeAsync();
        Assert.Equal([SyncTrigger.Start, SyncTrigger.Resume], _engine.Runs);
    }
}
